using bulk_insertt_api.Hubs;
using Microsoft.AspNetCore.SignalR;
using MySqlConnector;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace bulk_insertt_api
{
    public class ImportExecutor
    {
        private readonly IHubContext<ImportHub> hubContext;
        private readonly IWebHostEnvironment environment;
        public ImportExecutor(IHubContext<ImportHub> hubContext, IWebHostEnvironment webHostEnvironment)
        {
            this.hubContext = hubContext;
            this.environment = webHostEnvironment;
        }

        public async Task ExecuteAsync(string connectionId)
        {
            try
            {
                var connStr = "server=192.168.174.129;port=3306;database=Dict;uid=root;pwd=123456;AllowLoadLocalInfile=true;";
                var dicts = ReadCsvFile();
                using var dbConnection = new MySqlConnection(connStr);

                dbConnection.Open();

                var sqlBulkCopy = new MySqlBulkCopy(dbConnection, null);
                sqlBulkCopy.DestinationTableName = nameof(ECDict);
                var propertys = typeof(ECDict).GetProperties()
                     .Where(it => it.CanRead && it.GetCustomAttribute<NotMappedAttribute>() == null)
                     .ToList();
                for (int i = 0; i < propertys.Count; i++)
                {
                    var property = propertys[i];
                    var columnName = property.GetCustomAttribute<ColumnAttribute>()?.Name ?? property.Name;
                    sqlBulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, columnName));
                }

                // 每次处理100条
                int batchSize = 10000;
                int totalCount = dicts.Count;
                int counter = 0;
                for (int i = 0; i < totalCount; i += batchSize)
                {
                    int count = Math.Min(batchSize, totalCount - i);
                    counter += count;
                    var batch = dicts.GetRange(i, count);
                    var table = ToDataTable(batch);
                    await sqlBulkCopy.WriteToServerAsync(table);
                    await hubContext.Clients.Client(connectionId).SendAsync("ImportProgress", totalCount, counter);
                    Console.WriteLine($"已上传：{counter}条");
                }
                await hubContext.Clients.Client(connectionId).SendAsync("ImportState", "上传完成！");

            }
            catch (Exception ex)
            {
                await hubContext.Clients.Client(connectionId).SendAsync("ImportError", ex.Message);
                Console.WriteLine(ex.Message);
            }

        }

        /// <summary>
        ///  读取csv文件
        /// </summary>
        /// <returns></returns>
        private List<ECDict> ReadCsvFile()
        {
            var filePath = Path.Combine(environment.WebRootPath, "ecdict.csv");
            return File.ReadLines(filePath)
                .Skip(1)
                .Select(line =>
                {
                    var value = line.Split(',');
                    return new ECDict
                    {
                        Word = value[0],
                        Phonetic = value[1],
                        Definition = value[2],
                        Translation = value[3]
                    };
                }).ToList();
        }

        private static ConcurrentDictionary<string, object> CacheDictionary = new ConcurrentDictionary<string, object>();
        /// <summary>
        /// 构建一个object数据转换成一维数组数据的委托
        /// </summary>
        /// <param name="objType"></param>
        /// <param name="propertyInfos"></param>
        /// <returns></returns>
        private static Func<T, object[]> BuildObjectGetValuesDelegate<T>(List<PropertyInfo> propertyInfos) where T : class
        {
            var objParameter = Expression.Parameter(typeof(T), "model");
            var selectExpressions = propertyInfos.Select(it => BuildObjectGetValueExpression(objParameter, it));
            var arrayExpression = Expression.NewArrayInit(typeof(object), selectExpressions);
            var result = Expression.Lambda<Func<T, object[]>>(arrayExpression, objParameter).Compile();
            return result;
        }

        /// <summary>
        /// 构建对象获取单个值得
        /// </summary>
        /// <param name="modelExpression"></param>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        private static Expression BuildObjectGetValueExpression(ParameterExpression modelExpression, PropertyInfo propertyInfo)
        {
            var propertyExpression = Expression.Property(modelExpression, propertyInfo);
            var convertExpression = Expression.Convert(propertyExpression, typeof(object));
            return convertExpression;
        }

        private static DataTable ToDataTable<T>(IEnumerable<T> source, List<PropertyInfo> propertyInfos = null, bool useColumnAttribute = false) where T : class
        {
            var table = new DataTable("template");
            if (propertyInfos == null || propertyInfos.Count == 0)
            {
                propertyInfos = typeof(T).GetProperties().Where(it => it.CanRead).ToList();
            }
            foreach (var propertyInfo in propertyInfos)
            {
                var columnName = useColumnAttribute ? (propertyInfo.GetCustomAttribute<ColumnAttribute>()?.Name ?? propertyInfo.Name) : propertyInfo.Name;
                table.Columns.Add(columnName, ChangeType(propertyInfo.PropertyType));
            }

            Func<T, object[]> func;
            var key = typeof(T).FullName + propertyInfos.Select(it => it.Name).ToList();
            if (CacheDictionary.TryGetValue(key, out var cacheFunc))
            {
                func = (Func<T, object[]>)cacheFunc;
            }
            else
            {
                func = BuildObjectGetValuesDelegate<T>(propertyInfos);
                CacheDictionary.TryAdd(key, func);
            }

            foreach (var model in source)
            {
                var rowData = func(model);
                table.Rows.Add(rowData);
            }

            return table;
        }

        private static Type ChangeType(Type type)
        {
            if (IsNullableType(type))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            return type;
        }

        private static bool IsNullableType(Type theType)
        {
            return (theType.IsGenericType && theType.
              GetGenericTypeDefinition().Equals
              (typeof(Nullable<>)));
        }
    }
}

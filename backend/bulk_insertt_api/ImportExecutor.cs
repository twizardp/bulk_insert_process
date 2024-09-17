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
        public ImportExecutor(IHubContext<ImportHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        public async Task ExecuteAsync(string connectionId)
        {
            var connStr = "";
            var dicts = new List<ECDict>();
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
            var table = ToDataTable(dicts);

            await sqlBulkCopy.WriteToServerAsync(table);


        }

        public static ConcurrentDictionary<string, object> CacheDictionary = new ConcurrentDictionary<string, object>();
        /// <summary>
        /// 构建一个object数据转换成一维数组数据的委托
        /// </summary>
        /// <param name="objType"></param>
        /// <param name="propertyInfos"></param>
        /// <returns></returns>
        public static Func<T, object[]> BuildObjectGetValuesDelegate<T>(List<PropertyInfo> propertyInfos) where T : class
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
        public static Expression BuildObjectGetValueExpression(ParameterExpression modelExpression, PropertyInfo propertyInfo)
        {
            var propertyExpression = Expression.Property(modelExpression, propertyInfo);
            var convertExpression = Expression.Convert(propertyExpression, typeof(object));
            return convertExpression;
        }

        public static DataTable ToDataTable<T>(IEnumerable<T> source, List<PropertyInfo> propertyInfos = null, bool useColumnAttribute = false) where T : class
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

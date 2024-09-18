using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;

namespace bulk_insertt_api.Hubs
{
    [EnableCors("vueapp")]
    public class ImportHub : Hub
    {
        private readonly ImportExecutor _executor;

        public ImportHub(ImportExecutor executor)
        {
            _executor = executor;
        }

        public Task ImportExecute()
        {

            _ = _executor.ExecuteAsync(Context.ConnectionId);

            return Task.CompletedTask;
        }

    }
}

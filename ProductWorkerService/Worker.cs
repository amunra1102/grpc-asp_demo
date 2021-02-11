using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static ProductGrpc.Protos.ProductProtoService;

namespace ProductWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private readonly ProductFactory _productFactory;

        public Worker(ILogger<Worker> logger, IConfiguration config, ProductFactory productFactory)
        {
            _logger = logger;
            _config = config;
            _productFactory = productFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                try
                {
                    using var channel = GrpcChannel.ForAddress(_config.GetValue<string>("WorkerService:ServerUrl"));
                    var client = new ProductProtoServiceClient(channel);
                    _logger.LogInformation("AddProductAsync started..");
                    var addProductResponse = await client.AddProductAsync(await _productFactory.Generate());
                    _logger.LogInformation("AddProduct Response: {product}", addProductResponse.ToString());
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception.Message);
                    throw;
                }

                await Task.Delay(_config.GetValue<int>("WorkerService:TaskInterval"), stoppingToken);
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using ProductGrpc.Protos;

namespace ProductWorkerService
{
    public class ProductFactory
    {
        private readonly IConfiguration _config;

        public ProductFactory(IConfiguration config)
        {
            _config = config;
        }

        public Task<AddProductRequest> Generate()
        {
            var productName = $"{_config.GetValue<string>("WorkerService:ProductName")}_{DateTimeOffset.Now}";
            var productRequest = new AddProductRequest
            {
                Product = new ProductModel
                {
                    Name = productName,
                    Description = $"{productName}_Description",
                    Price = new Random().Next(1000),
                    Status = ProductStatus.Instock,
                    CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
                }
            };

            return Task.FromResult(productRequest);
        }
    }
}

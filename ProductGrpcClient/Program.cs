using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using ProductGrpc.Protos;
using static ProductGrpc.Protos.ProductProtoService;

namespace ProductGrpcClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var channel = GrpcChannel.ForAddress("https://localhost:5001");
            var client = new ProductProtoServiceClient(channel);

            // Get product
            await GetProductAsync(client);

            // Get all product
            await GetAllProducts(client);

            // Add Product
            await AddProductAsync(client);

            // Insert Bulk Product
            await InsertBulkProduct(client);

            // Update Product
            await UpdateProductAsync(client);

            // Delete Product
            await DeleteProductAsync(client);

            // Get all product
            await GetAllProducts(client);
        }

        private static async Task GetProductAsync(ProductProtoServiceClient client)
        {
            Console.WriteLine("GetProductAsync started...");
            var response = await client.GetProductAsync(new GetProductRequest(){ ProductId = 3 });
            Console.WriteLine($"GetProductAsync response: {response.ToString()}");
        }

        private static async Task GetAllProducts(ProductProtoServiceClient client)
        {
            Console.WriteLine("GetAllProducts started...");
            using var clientData = client.GetAllProducts(new GetAllProductsRequest());
            await foreach (var responseData in clientData.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine(responseData);
            }
            Thread.Sleep(1000);
        }

        private static async Task AddProductAsync(ProductProtoServiceClient client)
        {
            Console.WriteLine("AddProductAsync started...");
            var addProduct = await client.AddProductAsync(new AddProductRequest {
                Product = new ProductModel
                {
                    Name = "Red",
                    Description = "New Red Phone Mi10T",
                    Price = 699,
                    Status = ProductStatus.Instock,
                    CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
                }
            });

            Console.WriteLine($"AddProduct Response: {addProduct.ToString()}");
            Thread.Sleep(1000);
        }

        private static async Task UpdateProductAsync(ProductProtoServiceClient client)
        {
            // UpdateProductAsync
            Console.WriteLine("UpdateProductAsync started...");
            var updateProductResponse = await client.UpdateProductAsync(
                                 new UpdateProductRequest
                                 {
                                     Product = new ProductModel
                                     {
                                         ProductId = 2,
                                         Name = "Red",
                                         Description = "New Red Phone Mi10T",
                                         Price = 699,
                                         Status = ProductStatus.Instock,
                                         CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
                                     }
                                 });

            Console.WriteLine("UpdateProductAsync Response: " + updateProductResponse.ToString());
            Thread.Sleep(1000);
        }

        private static async Task DeleteProductAsync(ProductProtoServiceClient client)
        {
            // DeleteProductAsync
            Console.WriteLine("DeleteProductAsync started...");
            var deleteProductResponse = await client.DeleteProductAsync( new DeleteProductRequest { ProductId = 1 });

            Console.WriteLine("DeleteProductAsync Response: " + deleteProductResponse.Success.ToString());
            Thread.Sleep(1000);
        }

        private static async Task InsertBulkProduct(ProductProtoServiceClient client)
        {
            // InsertBulkProduct
            Console.WriteLine("InsertBulkProduct started...");
            using var clientBulk = client.InsertBulkProduct();

            for (var i = 0; i < 3; i++)
            {
                var productModel = new ProductModel
                {
                    Name = $"Product{i}",
                    Description = "Bulk inserted product",
                    Price = 399,
                    Status = ProductStatus.Instock,
                    CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
                };

                await clientBulk.RequestStream.WriteAsync(productModel);
            }
            await clientBulk.RequestStream.CompleteAsync();

            var responseBulk = await clientBulk;
            Console.WriteLine($"Status: {responseBulk.Success}. Insert Count: {responseBulk.InsertCount}");
            Thread.Sleep(1000);
        }
    }
}

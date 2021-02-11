using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using IdentityModel.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductGrpc.Protos;
using ShoppingCartGrpc.Protos;

namespace ShoppingCartWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                using var scChannel = GrpcChannel.ForAddress(_config.GetValue<string>("WorkerService:ShoppingCartServerUrl"));
                var scClient = new ShoppingCartProtoService.ShoppingCartProtoServiceClient(scChannel);

                // 1.Get Token from IS4
                var token = await GetTokenFromIS4();

                // 2.Create SC if not exist
                await GetOrCreateShoppingCartAsync(scClient, token);

                // open sc client stream
                using var scClientStream = scClient.AddItemIntoShoppingCart();

                // 3.Retrieve products from product grpc with server stream
                using var productChannel = GrpcChannel.ForAddress(_config.GetValue<string>("WorkerService:ProductServerUrl"));
                var productClient = new ProductProtoService.ProductProtoServiceClient(productChannel);

                _logger.LogInformation("GetAllProducts started..");
                using var clientData = productClient.GetAllProducts(new GetAllProductsRequest());
                await foreach (var responseData in clientData.ResponseStream.ReadAllAsync())
                {
                    _logger.LogInformation($"GetAllProducts Stream Response: {responseData}");

                    // 4.Add sc items into SC with client stream
                    var addNewScItem = new AddItemIntoShoppingCartRequest
                    {
                        Username = _config.GetValue<string>("WorkerService:UserName"),
                        DiscountCode = "CODE_100",
                        NewCartItem = new ShoppingCartItemModel
                        {
                            ProductId = responseData.ProductId,
                            Productname = responseData.Name,
                            Price = responseData.Price,
                            Color = "Black",
                            Quantity = 1
                        }
                    };

                    await scClientStream.RequestStream.WriteAsync(addNewScItem);
                    _logger.LogInformation($"ShoppingCart Client Stream Added New Item : {addNewScItem}");
                }
                await scClientStream.RequestStream.CompleteAsync();

                var addItemIntoShoppingCartResponse = await scClientStream;
                _logger.LogInformation($"AddItemIntoShoppingCart Client Stream Response: {addItemIntoShoppingCartResponse}");

                await Task.Delay(_config.GetValue<int>("WorkerService:TaskInterval"), stoppingToken);
            }
        }

        private async Task<string> GetTokenFromIS4()
        {
            _logger.LogInformation("GetTokenFromIS4 Started..");

            // discover endpoints from metadata
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(_config.GetValue<string>("WorkerService:IdentityServerUrl"));
            if (disco.IsError)
            {
                _logger.LogError(disco.Error);
                return string.Empty;
            }

            _logger.LogInformation($"Discovery endpoint taken from IS4 metadata. Discovery : {disco.TokenEndpoint}");

            // request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,

                ClientId = "ShoppingCartClient",
                ClientSecret = "secret",
                Scope = "ShoppingCartAPI"
            });

            if (tokenResponse.IsError)
            {
                _logger.LogError(tokenResponse.Error);
                return string.Empty;
            }

            _logger.LogInformation($"Token retrieved for IS4. Token : {tokenResponse.AccessToken}");

            return tokenResponse.AccessToken;
        }

        private async Task GetOrCreateShoppingCartAsync(ShoppingCartProtoService.ShoppingCartProtoServiceClient scClient, string token)
        {
            ShoppingCartModel shoppingCartModel;
            _logger.LogInformation($"UserName: {_config.GetValue<string>("WorkerService:UserName")}");
            try
            {
                _logger.LogInformation($"GetShoppingCartAsync started...");

                var headers = new Metadata();
                headers.Add("Authorization", $"Bearer {token}");

                shoppingCartModel = await scClient.GetShoppingCartAsync(new GetShoppingCartRequest { Username = _config.GetValue<string>("WorkerService:UserName") }, headers);

                _logger.LogInformation($"GetShoppingCartAsync Response: {shoppingCartModel}");
            }
            catch (RpcException exception)
            {
                if (exception.StatusCode == StatusCode.NotFound)
                {
                    _logger.LogInformation("CreateShoppingCartAsync started..");
                    shoppingCartModel = await scClient.CreateShoppingCartAsync(new ShoppingCartModel { Username = _config.GetValue<string>("WorkerService:UserName") });
                    _logger.LogInformation($"CreateShoppingCartAsync Response: {shoppingCartModel}");
                }
                else
                {
                    _logger.LogError($"Error: {exception.Message}");
                    throw;
                }
            }
        }
    }
}

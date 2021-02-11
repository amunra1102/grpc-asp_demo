using System.Threading.Tasks;
using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductGrpc.Data;
using ProductGrpc.Models;
using ProductGrpc.Protos;

namespace ProductGrpc.Services
{
    public class ProductService : ProductProtoService.ProductProtoServiceBase
    {
        private readonly ProductsContext _productsContext;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductService> _logger;

        public ProductService(ProductsContext productsContext, IMapper mapper, ILogger<ProductService> logger)
        {
            _productsContext = productsContext;
            _mapper = mapper;
            _logger = logger;
        }

        public override Task<Empty> Test(Empty request, ServerCallContext context)
        {
            return base.Test(request, context);
        }

        public override async Task<ProductModel> GetProduct(GetProductRequest request, ServerCallContext context)
        {
            var product = await _productsContext.Product.FindAsync(request.ProductId);

            if (null == product)
            {
                _logger.LogError($"Error: Product with ID = {request.ProductId} is not found.");
                return new ProductModel();
            }

            return _mapper.Map<ProductModel>(product);
        }

        public override async Task GetAllProducts(GetAllProductsRequest request, IServerStreamWriter<ProductModel> responseStream, ServerCallContext context)
        {
            var productList = await _productsContext.Product.ToListAsync();

            foreach (var product in productList)
            {
                await responseStream.WriteAsync(_mapper.Map<ProductModel>(product));
            }
        }

        public override async Task<ProductModel> AddProduct(AddProductRequest request, ServerCallContext context)
        {
            // 1. Mapping ProductModel to Product
            var product = _mapper.Map<Product>(request.Product);

            // 2. Add Product to Database
            _productsContext.Product.Add(product);
            await _productsContext.SaveChangesAsync();

            _logger.LogInformation($"Infomation: Product successfully added : {product.ProductId} - { product.Name}");

            // 3. Resturn response: ProductModel
            return _mapper.Map<ProductModel>(product);
        }

        public override async Task<ProductModel> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
        {
            // 1. Mapping ProductModel to Product
            var product = _mapper.Map<Product>(request.Product);

            bool isExist = await _productsContext.Product.AnyAsync(p => p.ProductId == product.ProductId);
            if (!isExist)
            {
                _logger.LogError($"Error: Product with ID = {product.ProductId} is not found.");
                return new ProductModel();
            }

            _productsContext.Entry(product).State = EntityState.Modified;

            try
            {
                // 2. Update Product to Database
                await _productsContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogError($"Error: {exception.Message}");
                return new ProductModel();
            }

            // 3. Resturn response: ProductModel
            return _mapper.Map<ProductModel>(product);
        }

        public override async Task<DeleteProductResponse> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
        {
            var product = await _productsContext.Product.FindAsync(request.ProductId);
            if (product == null)
            {
                _logger.LogError($"Product with ID = {request.ProductId} is not found.");
                return new DeleteProductResponse { Success = false };
            }

            _productsContext.Product.Remove(product);
            var deleteCount = await _productsContext.SaveChangesAsync();

            return new DeleteProductResponse { Success = deleteCount > 0 };
        }

        public override async Task<InsertBulkProductResponse> InsertBulkProduct(IAsyncStreamReader<ProductModel> requestStream, ServerCallContext context)
        {
            await foreach (var requestData in requestStream.ReadAllAsync())
            {
                _productsContext.Product.Add(_mapper.Map<Product>(requestData));
            }

            var insertCount = await _productsContext.SaveChangesAsync();

            return new InsertBulkProductResponse
            {
                Success = insertCount > 0,
                InsertCount = insertCount
            };
        }
    }
}

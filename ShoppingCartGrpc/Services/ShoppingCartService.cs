using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingCartGrpc.Data;
using ShoppingCartGrpc.Models;
using ShoppingCartGrpc.Protos;

namespace ShoppingCartGrpc.Services
{
    [Authorize]
    public class ShoppingCartService : ShoppingCartProtoService.ShoppingCartProtoServiceBase
    {
        private readonly ShoppingCartContext _shoppingCartContext;
        private readonly DiscountService _discountService;
        private readonly IMapper _mapper;
        private readonly ILogger<ShoppingCartService> _logger;

        public ShoppingCartService(ShoppingCartContext shoppingCartContext, DiscountService discountService, IMapper mapper, ILogger<ShoppingCartService> logger)
        {
            _shoppingCartContext = shoppingCartContext;
            _discountService = discountService;
            _mapper = mapper;
            _logger = logger;
        }

        public override async Task<ShoppingCartModel> GetShoppingCart(GetShoppingCartRequest request, ServerCallContext context)
        {
            var shoppingCart = await _shoppingCartContext.ShoppingCart.FirstOrDefaultAsync(s => s.UserName == request.Username);
            _logger.LogInformation($"ShoppingCart: {shoppingCart}");
            if (shoppingCart == null)
            {
                _logger.LogError($"ShoppingCart with UserName = {request.Username} is not found.");
                throw new RpcException(new Status(StatusCode.NotFound, $"ShoppingCart with UserName = {request.Username} is not found."));
            }

            return _mapper.Map<ShoppingCartModel>(shoppingCart);
        }

        public override async Task<ShoppingCartModel> CreateShoppingCart(ShoppingCartModel request, ServerCallContext context)
        {
            var shoppingCart = _mapper.Map<ShoppingCart>(request);

            var isExist = await _shoppingCartContext.ShoppingCart.AnyAsync(s => s.UserName == shoppingCart.UserName);
            if (isExist)
            {
                _logger.LogError($"Invalid UserName for ShoppingCart creation. UserName : {shoppingCart.UserName}");
                throw new RpcException(new Status(StatusCode.NotFound, $"ShoppingCart with UserName = {request.Username} is already exist."));
            }

            _shoppingCartContext.ShoppingCart.Add(shoppingCart);
            await _shoppingCartContext.SaveChangesAsync();

            _logger.LogInformation($"ShoppingCart is successfully created.UserName : {shoppingCart.UserName}");

            return _mapper.Map<ShoppingCartModel>(shoppingCart);
        }

        [AllowAnonymous]
        public override async Task<AddItemIntoShoppingCartResponse> AddItemIntoShoppingCart(IAsyncStreamReader<AddItemIntoShoppingCartRequest> requestStream, ServerCallContext context)
        {
            await foreach (var requestData in requestStream.ReadAllAsync())
            {
                var shoppingCart = await _shoppingCartContext.ShoppingCart.FirstOrDefaultAsync(s => s.UserName == requestData.Username);
                if (shoppingCart == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, $"ShoppingCart with UserName = {requestData.Username} is not found."));
                }

                var newAddedCartItem = _mapper.Map<ShoppingCartItem>(requestData.NewCartItem);
                var cartItem = shoppingCart.Items.FirstOrDefault(i => i.ProductId == newAddedCartItem.ProductId);
                if (null != cartItem)
                {
                    cartItem.Quantity++;
                }
                else
                {
                    // GRPC CALL DISCOUNT SERVICE -- check discount and set the item price
                    var discount = await _discountService.GetDiscount(requestData.DiscountCode);
                    newAddedCartItem.Price -= discount.Amount;

                    shoppingCart.Items.Add(newAddedCartItem);
                }
            }

            var insertCount = await _shoppingCartContext.SaveChangesAsync();

            var response = new AddItemIntoShoppingCartResponse
            {
                Success = insertCount > 0,
                InsertCount = insertCount
            };

            return response;
        }

        [AllowAnonymous]
        public override async Task<RemoveItemIntoShoppingCartResponse> RemoveItemIntoShoppingCart(RemoveItemIntoShoppingCartRequest request, ServerCallContext context)
        {
            var shoppingCart = await _shoppingCartContext.ShoppingCart.FirstOrDefaultAsync(s => s.UserName == request.Username);
            if (shoppingCart == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"ShoppingCart with UserName = {request.Username} is not found."));
            }

            var removeCartItem = shoppingCart.Items.FirstOrDefault(i => i.ProductId == request.RemoveCartItem.ProductId);
            if (null == removeCartItem)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"CartItem with ProductId = {request.RemoveCartItem.ProductId} is not found in the ShoppingCart."));
            }

            shoppingCart.Items.Remove(removeCartItem);

            var removeCount = await _shoppingCartContext.SaveChangesAsync();

            var response = new RemoveItemIntoShoppingCartResponse
            {
                Success = removeCount > 0
            };

            return response;
        }
    }
}

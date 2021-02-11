using System.Threading.Tasks;
using DiscountGrpc.Protos;

namespace ShoppingCartGrpc.Services
{
    public class DiscountService
    {
        private readonly DiscountProtoService.DiscountProtoServiceClient _discountProtoService;

        public DiscountService(DiscountProtoService.DiscountProtoServiceClient discountProtoService)
        {
            _discountProtoService = discountProtoService;
        }

        public async Task<DiscountModel> GetDiscount(string discountCode)
        {
            var discountRequest = new GetDiscountRequest { DiscountCode = discountCode };

            return await _discountProtoService.GetDiscountAsync(discountRequest);
        }
    }
}

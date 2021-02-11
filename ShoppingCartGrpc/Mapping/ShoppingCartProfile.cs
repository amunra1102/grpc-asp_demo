using AutoMapper;
using ShoppingCartGrpc.Models;
using ShoppingCartGrpc.Protos;

namespace ShoppingCartGrpc.Mapping
{
    public class ShoppingCartProfile : Profile
    {
        public ShoppingCartProfile()
        {
            CreateMap<ShoppingCart, ShoppingCartModel>().ReverseMap();
            CreateMap<ShoppingCartItem, ShoppingCartItemModel>().ReverseMap();
        }
    }
}

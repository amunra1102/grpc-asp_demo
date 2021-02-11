using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using ProductGrpc.Models;
using ProductGrpc.Protos;

namespace ProductGrpc.Mapping
{
    public class ProductProfile : Profile
    {
        public ProductProfile()
        {
            CreateMap<Product, ProductModel>()
                .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => Timestamp.FromDateTime(src.CreatedTime)));

            CreateMap<ProductModel, Product>()
                .ForMember(dest => dest.CreatedTime, opt => opt.MapFrom(src => src.CreatedTime.ToDateTime()));

            // note : not use reverseMap. Timestamp should be converted manually.
        }
    }
}

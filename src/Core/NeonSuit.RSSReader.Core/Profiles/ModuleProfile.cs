using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Modules;
using NeonSuit.RSSReader.Core.Interfaces.Modules;
using NeonSuit.RSSReader.Core.Modules;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for module-related entities.
    /// </summary>
    public class ModuleProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleProfile"/> class.
        /// </summary>
        public ModuleProfile()
        {
            // IModule -> ModuleInfoDto
            CreateMap<IModule, ModuleInfoDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Version, opt => opt.MapFrom(src => src.Version.ToString()))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.Author))
                .ForMember(dest => dest.Dependencies, opt => opt.MapFrom(src => src.Dependencies))
                .ForMember(dest => dest.HasConfig, opt => opt.MapFrom(src => src is IConfigurableModule))
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.ModuleType, opt => opt.Ignore())
                .ForMember(dest => dest.AssemblyLocation, opt => opt.Ignore())
                .ForMember(dest => dest.LoadTime, opt => opt.Ignore());

            // ModuleConfigSchema -> ModuleConfigSchemaDto
            CreateMap<ModuleConfigSchema, ModuleConfigSchemaDto>();

            // ModuleConfigProperty -> ModuleConfigPropertyDto
            CreateMap<ModuleConfigProperty, ModuleConfigPropertyDto>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.AllowedValues, opt => opt.MapFrom(src => src.AllowedValues != null ? src.AllowedValues.ToList() : null));
        }
    }
}
using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Opml;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for OPML
    /// </summary>
    public class OpmlProfile : Profile
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="OpmlProfile"/> class.
        /// </summary>
        public OpmlProfile()
        {
            CreateMap<OpmlStatisticsDto, OpmlStatisticsDto>();

            // Note: Most OPML operations don't require mapping from entities
            // as they work with streams and XML directly
        }
    }
}
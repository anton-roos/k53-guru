namespace K53Guru.Application.Features.RoadSigns.DTOs;

[Description("Road Signs")]
public class RoadSignDto
{
    [Description("Id")] public int Id { get; set; }

    [Description("Legislation Code")] public string? LegislationCode { get; set; }

    [Description("Description")] public string? Description { get; set; }

    [Description("Image")] public string? ImageAssetKey { get; set; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<RoadSign, RoadSignDto>().ReverseMap();
        }
    }
}

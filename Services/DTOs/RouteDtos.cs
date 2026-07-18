using System.ComponentModel.DataAnnotations;

namespace Services.DTOs;

public sealed class RouteCoordinateDto
{
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [MaxLength(250)]
    public string? Label { get; set; }
}

public sealed class ComputeRouteRequestDto
{
    [Required]
    public RouteCoordinateDto? Origin { get; set; }

    [Required]
    public RouteCoordinateDto? Destination { get; set; }

    public bool AvoidHighways { get; set; }
    public bool AvoidTolls { get; set; }
    public bool AvoidFerries { get; set; }
}

public sealed class RouteResultDto
{
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
    public List<double[]> Coordinates { get; set; } = [];
    public double[] BoundingBox { get; set; } = [];
    public List<RouteStepDto> Steps { get; set; } = [];
}

public sealed class RouteStepDto
{
    public string Instruction { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
    public int ManeuverType { get; set; }
}

public sealed class PlaceSuggestionDto
{
    public string Label { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Locality { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? DistanceMeters { get; set; }
}

public sealed class RouteAssistantRequestDto
{
    [Required]
    [MinLength(3)]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}

public sealed class RouteAssistantResponseDto
{
    public string AssistantMessage { get; set; } = string.Empty;
    public string SearchQuery { get; set; } = string.Empty;
    public bool UsedAi { get; set; }
    public List<PlaceSuggestionDto> Places { get; set; } = [];
}

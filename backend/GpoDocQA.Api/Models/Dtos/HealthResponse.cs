namespace GpoDocQA.Api.Models.Dtos;

public record HealthResponse(string Status, string Environment, DateTimeOffset ServerTimeUtc);

namespace EconAdvisor.Api.Models;

public sealed record AnalyseRequest(string Question, string Country = "SE");

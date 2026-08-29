using System.ComponentModel;

namespace K53Guru.Domain.Enums;

public enum SecurityRiskLevel
{
    [Description("Low")]
    Low = 1,
    
    [Description("Medium")]
    Medium = 2,
    
    [Description("High")]
    High = 3,
    
    [Description("Critical")]
    Critical = 4
} 

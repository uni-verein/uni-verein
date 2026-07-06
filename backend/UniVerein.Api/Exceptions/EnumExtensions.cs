using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace UniVerein.Api.Exceptions;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        
        var attribute = field?.GetCustomAttribute<DisplayAttribute>();
        
        return attribute?.Name ?? value.ToString();
    }
}
using System;
using System.Collections.Generic;
using FoodBot.Services; // MatchedFoodRow

namespace FoodBot.Models
{
    /// <summary>
    /// Stores all data related to a nutrition analysis session.
    /// </summary>
    public class NutritionSession
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string ImageDataUrl { get; set; } = "";
        public string[] Ingredients { get; set; } = Array.Empty<string>();
        public MatchedFoodRow[] MatchedFoods { get; set; } = Array.Empty<MatchedFoodRow>();
        public Step1Snapshot? Step1 { get; set; }
        public List<object> History { get; } = new();
    }
}

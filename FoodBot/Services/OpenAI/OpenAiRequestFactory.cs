using System.Collections.Generic;

namespace FoodBot.Services.OpenAI
{
    public static class OpenAiRequestFactory
    {
        public static object BuildVisionStep1Request(string model, System.Collections.Generic.List<object> messages)
        {
            return new
            {
                model = model,
                messages = messages,
                response_format = new
                {
                    type = "json_schema",
                    json_schema = new
                    {
                        name = "vision_step1",
                        schema = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                dish = new { type = "string" },
                                ingredients = new { type = "array", items = new { type = "string" } },
                                shares_percent = new { type = "array", items = new { type = "number" } },
                                weight_g = new { type = "number", minimum = 0 },
                                confidence = new { type = "number", minimum = 0, maximum = 1 }
                            },
                            required = new[] { "dish", "ingredients", "shares_percent", "weight_g", "confidence" }
                        }
                    }
                },
                temperature = 1
            };
        }

        public static object BuildFinalResponseFormatSchema()
        {
            return new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "final_nutrition",
                    schema = new
                    {
                        type = "object",
                        additionalProperties = false,
                        properties = new
                        {
                            final = new
                            {
                                type = "object",
                                additionalProperties = false,
                                properties = new
                                {
                                    dish = new { type = "string" },
                                    weight_g = new { type = "number", minimum = 0 },
                                    proteins_g = new { type = "number", minimum = 0 },
                                    fats_g = new { type = "number", minimum = 0 },
                                    carbs_g = new { type = "number", minimum = 0 },
                                    calories_kcal = new { type = "number", minimum = 0 },
                                    confidence = new { type = "number", minimum = 0, maximum = 1 },
                                    per_ingredient = new
                                    {
                                        type = "array",
                                        minItems = 1,
                                        items = new
                                        {
                                            type = "object",
                                            additionalProperties = false,
                                            properties = new
                                            {
                                                name = new { type = "string" },
                                                grams = new { type = "number", minimum = 0 },
                                                per100g_proteins_g = new { type = "number", minimum = 0 },
                                                per100g_fats_g = new { type = "number", minimum = 0 },
                                                per100g_carbs_g = new { type = "number", minimum = 0 },
                                                kcal_per_g = new { type = "number", minimum = 0 }
                                            },
                                            required = new[] { "name", "grams", "per100g_proteins_g", "per100g_fats_g", "per100g_carbs_g", "kcal_per_g" }
                                        }
                                    }
                                },
                                required = new[] { "dish", "weight_g", "proteins_g", "fats_g", "carbs_g", "calories_kcal", "confidence", "per_ingredient" }
                            }
                        },
                        required = new[] { "final" }
                    }
                }
            };
        }

        public static object BuildFinalRequest(string model, IEnumerable<object> messages, object responseFormatSchema)
        {
            return new
            {
                model = model,
                messages = messages,
                response_format = responseFormatSchema
            };
        }
    }
}
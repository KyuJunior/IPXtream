using System;
using System.IO;
using Newtonsoft.Json;
using IPXtream.Models;

class Program
{
    static void Main()
    {
        try
        {
            string cachePath = @"C:\Users\Dr. Yaser\AppData\Local\IPXtream\Cache\99a52325c2b722a8537e4b464cb2d2ed.json";
            Console.WriteLine($"Reading cache from: {cachePath}");
            string json = File.ReadAllText(cachePath);
            Console.WriteLine($"JSON length: {json.Length}");
            
            var response = JsonConvert.DeserializeObject<SeriesInfoResponse>(json);
            if (response == null)
            {
                Console.WriteLine("Deserialized response is null.");
                return;
            }
            
            Console.WriteLine($"Series Name: {response.Info?.Name}");
            Console.WriteLine($"Episodes dictionary count: {response.Episodes?.Count}");
            if (response.Episodes != null)
            {
                foreach (var kvp in response.Episodes)
                {
                    Console.WriteLine($"Season {kvp.Key}: {kvp.Value?.Count} episodes");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex}");
        }
    }
}

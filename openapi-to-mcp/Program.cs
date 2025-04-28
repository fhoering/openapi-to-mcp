using System.Globalization;
using DotMake.CommandLine;
using OpenApiToMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        
        try
        {
            await Cli.RunAsync<Command>(args);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Unmanaged exception: " + e.Message);
        }
    }
}
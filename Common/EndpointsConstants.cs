namespace Common
{
     public class EndpointsConstants
     {
          // Poate fi schimbata fara recompilare prin variabila de mediu BROKER_ADDRESS (vezi .env.example).
          public static readonly string BrokerAddress = Environment.GetEnvironmentVariable("BROKER_ADDRESS") ?? "https://localhost:5001";

          public const string SubscribersAddress = "https://127.0.0.1:0";

     }
}

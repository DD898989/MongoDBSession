using NBomber.CSharp;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Collections.Concurrent;

namespace MongoDBSession.LoadTests;

class Program
{
    private const int LoginInterval = 20; // Ratio of 1 Login per 20 Profile requests

    static async Task Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   Starting MongoDBSession NBomber Load Test");
        Console.WriteLine("=================================================");

        Console.WriteLine("Select stress test scenario:");
        Console.WriteLine("1. JWT Authentication (Login & Profile Access)");
        Console.WriteLine("2. Redis Session (Login & Profile Access)");
        Console.WriteLine("3. MongoDB Session (Login & Profile Access)");
        Console.WriteLine("4. Run All Scenarios");
        Console.Write("Enter choice (1-4, default 4): ");
        var choice = Console.ReadLine()?.Trim() ?? "4";
        if (string.IsNullOrEmpty(choice)) choice = "4";

        var scenarios = new List<NBomber.Contracts.ScenarioProps>();
        using var httpClient = new HttpClient();

        // Thread-safe dictionaries to cache session tokens per virtual user (ThreadNumber)
        var jwtTokens = new ConcurrentDictionary<int, string>();
        var redisTokens = new ConcurrentDictionary<int, string>();
        var mongoTokens = new ConcurrentDictionary<int, string>();

        if (choice == "1" || choice == "4")
        {
            var jwtScenario = Scenario.Create("jwt_auth_scenario", async context =>     
            {
                var instanceId = context.ScenarioInfo.ThreadNumber;
                string? token = null;

                // Determine if we need to login:
                // 1. Token doesn't exist in cache for this virtual user.
                // 2. Or we hit the periodic login interval (e.g., every 20 invocations).
                bool needLogin = !jwtTokens.TryGetValue(instanceId, out token) 
                                 || (context.InvocationNumber % LoginInterval == 1);

                if (needLogin)
                {
                    var loginStep = await Step.Run("jwt_login", context, async () =>
                    {
                        try
                        {
                            // 1. Login to get JWT
                            var loginResponse = await httpClient.PostAsync("http://127.0.0.1:8080/api/jwt/login", null);
                            if (!loginResponse.IsSuccessStatusCode)
                            {
                                return Response.Fail(statusCode: ((int)loginResponse.StatusCode).ToString(), message: "Login failed");
                            }

                            var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
                            var newToken = loginResult.TryGetProperty("token", out var tokenProp)
                                ? tokenProp.GetString()
                                : (loginResult.TryGetProperty("Token", out var tokenPropPascal) ? tokenPropPascal.GetString() : null);

                            if (string.IsNullOrEmpty(newToken))
                            {
                                return Response.Fail(statusCode: "NoToken", message: "Token is empty");
                            }

                            jwtTokens[instanceId] = newToken;
                            return Response.Ok(statusCode: "200");
                        }
                        catch (Exception ex)
                        {
                            return Response.Fail(statusCode: "Exception", message: ex.Message);
                        }
                    });

                    if (loginStep.IsError)
                    {
                        return Response.Fail(statusCode: loginStep.StatusCode, message: loginStep.Message);
                    }

                    token = jwtTokens[instanceId];
                }

                // 2. Fetch Profile using the cached JWT
                var profileStep = await Step.Run("jwt_profile", context, async () =>
                {
                    try
                    {
                        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:8080/api/jwt/profile");
                        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        var profileResponse = await httpClient.SendAsync(profileRequest);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            return Response.Ok(statusCode: "200");
                        }
                        else
                        {
                            return Response.Fail(statusCode: ((int)profileResponse.StatusCode).ToString(), message: "Profile access failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(statusCode: "Exception", message: ex.Message);
                    }
                });

                return Response.Ok();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                Simulation.RampingConstant(copies: 1000, during: TimeSpan.FromMinutes(1)),
                Simulation.KeepConstant(copies: 1000, during: TimeSpan.FromMinutes(1))
            );

            scenarios.Add(jwtScenario);
        }

        if (choice == "2" || choice == "4")
        {
            var redisScenario = Scenario.Create("redis_session_scenario", async context =>
            {
                var instanceId = context.ScenarioInfo.ThreadNumber;
                string? token = null;

                bool needLogin = !redisTokens.TryGetValue(instanceId, out token) 
                                 || (context.InvocationNumber % LoginInterval == 1);

                if (needLogin)
                {
                    var loginStep = await Step.Run("redis_login", context, async () =>
                    {
                        try
                        {
                            // 1. Login to get Redis Session Token
                            var loginResponse = await httpClient.PostAsync("http://127.0.0.1:8080/api/redis/login", null);
                            if (!loginResponse.IsSuccessStatusCode)
                            {
                                return Response.Fail(statusCode: ((int)loginResponse.StatusCode).ToString(), message: "Redis login failed");
                            }

                            var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
                            var newToken = loginResult.TryGetProperty("token", out var tokenProp)
                                ? tokenProp.GetString()
                                : (loginResult.TryGetProperty("Token", out var tokenPropPascal) ? tokenPropPascal.GetString() : null);

                            if (string.IsNullOrEmpty(newToken))
                            {
                                return Response.Fail(statusCode: "NoToken", message: "Token is empty");
                            }

                            redisTokens[instanceId] = newToken;
                            return Response.Ok(statusCode: "200");
                        }
                        catch (Exception ex)
                        {
                            return Response.Fail(statusCode: "Exception", message: ex.Message);
                        }
                    });

                    if (loginStep.IsError)
                    {
                        return Response.Fail(statusCode: loginStep.StatusCode, message: loginStep.Message);
                    }

                    token = redisTokens[instanceId];
                }

                // 2. Fetch Profile using the cached Redis Session Token
                var profileStep = await Step.Run("redis_profile", context, async () =>
                {
                    try
                    {
                        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:8080/api/redis/profile");
                        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        var profileResponse = await httpClient.SendAsync(profileRequest);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            return Response.Ok(statusCode: "200");
                        }
                        else
                        {
                            return Response.Fail(statusCode: ((int)profileResponse.StatusCode).ToString(), message: "Redis profile access failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(statusCode: "Exception", message: ex.Message);
                    }
                });

                return Response.Ok();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                Simulation.RampingConstant(copies: 1000, during: TimeSpan.FromMinutes(1)),
                Simulation.KeepConstant(copies: 1000, during: TimeSpan.FromMinutes(1))
            );

            scenarios.Add(redisScenario);
        }

        if (choice == "3" || choice == "4")
        {
            var mongoScenario = Scenario.Create("mongodb_session_scenario", async context =>
            {
                var instanceId = context.ScenarioInfo.ThreadNumber;
                string? token = null;

                bool needLogin = !mongoTokens.TryGetValue(instanceId, out token) 
                                 || (context.InvocationNumber % LoginInterval == 1);

                if (needLogin)
                {
                    var loginStep = await Step.Run("mongodb_login", context, async () =>        
                    {
                        try
                        {
                            // 1. Login to get MongoDB Session Token
                            var loginResponse = await httpClient.PostAsync("http://127.0.0.1:8080/api/mongodb/login", null);
                            if (!loginResponse.IsSuccessStatusCode)
                            {
                                return Response.Fail(statusCode: ((int)loginResponse.StatusCode).ToString(), message: "MongoDB login failed");
                            }

                            var loginResult = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
                            var newToken = loginResult.TryGetProperty("token", out var tokenProp)
                                ? tokenProp.GetString()
                                : (loginResult.TryGetProperty("Token", out var tokenPropPascal) ? tokenPropPascal.GetString() : null);

                            if (string.IsNullOrEmpty(newToken))
                            {
                                return Response.Fail(statusCode: "NoToken", message: "Token is empty");
                            }

                            mongoTokens[instanceId] = newToken;
                            return Response.Ok(statusCode: "200");
                        }
                        catch (Exception ex)
                        {
                            return Response.Fail(statusCode: "Exception", message: ex.Message);
                        }
                    });

                    if (loginStep.IsError)
                    {
                        return Response.Fail(statusCode: loginStep.StatusCode, message: loginStep.Message);
                    }

                    token = mongoTokens[instanceId];
                }

                // 2. Fetch Profile using the cached MongoDB Session Token
                var profileStep = await Step.Run("mongodb_profile", context, async () =>
                {
                    try
                    {
                        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:8080/api/mongodb/profile");
                        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        var profileResponse = await httpClient.SendAsync(profileRequest);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            return Response.Ok(statusCode: "200");
                        }
                        else
                        {
                            return Response.Fail(statusCode: ((int)profileResponse.StatusCode).ToString(), message: "MongoDB profile access failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        return Response.Fail(statusCode: "Exception", message: ex.Message);
                    }
                });

                return Response.Ok();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                Simulation.RampingConstant(copies: 1000, during: TimeSpan.FromMinutes(1)),
                Simulation.KeepConstant(copies: 1000, during: TimeSpan.FromMinutes(1))
            );

            scenarios.Add(mongoScenario);
        }

        if (scenarios.Count > 0)
        {
            NBomberRunner
                .RegisterScenarios(scenarios.ToArray())
                .Run();
        }
        else
        {
            Console.WriteLine("No scenarios selected.");
        }
    }
}

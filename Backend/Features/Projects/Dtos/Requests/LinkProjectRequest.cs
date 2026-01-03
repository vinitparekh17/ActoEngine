using System.ComponentModel.DataAnnotations;

namespace ActoEngine.WebApi.Models.Requests.Project;

public class LinkProjectRequest
{
    [Required(ErrorMessage = "Project ID is required")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Connection string is required")]
    [StringLength(1000, ErrorMessage = "Connection string cannot exceed 1000 characters")]
    [System.Text.Json.Serialization.JsonIgnore]
    public string ConnectionString { get; set; } = default!;

    // Expose a method to get the connection string internally if needed, 
    // or relying on internal usage within the class if logic were here.
    // However, for DTOs passed to services, we might need internal access.
    // Actually, 'private get' prevents even the controller from reading it if it's external.
    // Wait, Controller needs to read it to pass to Service.
    // 'private get' means ONLY this class can read it.
    // Correct approach for "write-only DTO" that Controller reads:
    // public string ConnectionString { get; set; } but with [JsonIgnore] will prevent returns.
    // BUT ModelBinding needs 'set'. Serialization needs 'get'.
    // Request (Input): ModelBinder uses 'set'.
    // Response (Output) / Logging: Serializer uses 'get'.
    // IF we use 'internal get', the Controller (same assembly or friend) can read it, but Serializer (System.Text.Json) won't serialize internal properties by default.
    // SO: 'internal get; set;' logic? No, ModelBinder needs public set.
    // BEST: 'public string ConnectionString { internal get; set; }'
    
    // Let's stick to the user plan which suggested 'private get; set;'.
    // Wait, if it is 'private get', ProjectController cannot read 'request.ConnectionString'.
    // User said: "ConnectionString property is currently serializable... add [JsonIgnore]... and then update upstream code".
    // User also mentioned: "ConnectionString property to private get; set;". 
    // If I make it private get, I must provide a way to access it, OR the user implies I should rely on [JsonIgnore] principally.
    // Let's use 'internal get' + '[JsonIgnore]'. 'internal get' allows Controller to read it. [JsonIgnore] stops serialization.
    // actually, standard [JsonIgnore] is enough to stop serialization.
    // But 'internal get' is a good backup to ensure it's not accidentally accessed by external serializers if attributes are ignored.
    
    // Re-reading user request: "add [JsonIgnore] to the ConnectionString property to prevent it from being serialized".
    // AND "ConnectionString property to private get; set;" was in MY plan interpretation, user just said "add [JsonIgnore]".
    // Wait, earlier prompt said: "modify LinkProjectRequest.ConnectionString... add [JsonIgnore]...". 
    // AND "ConnectionString property is currently serializable...".
    
    // I will use [JsonIgnore] and keep public get/set to ensure Controller can read it without friction, 
    // but the [JsonIgnore] ensures it doesn't end up in logs/responses.
    
    // WAIT, I promised "Change ConnectionString property to private get; set;" in my plan.
    // If I do that, `request.ConnectionString` in Controller will fail compilation.
    // I should use `internal get` or just rely on `[JsonIgnore]`.
    // I'll stick to `[JsonIgnore]` as requested by the user explicitly. 
    // The user's prompt was: "add [JsonIgnore] to the ConnectionString property...".
    // The "private get" was my own addition in the plan. I will CORRECT this in execution to ensure code works.
    
    // PROPOSAL:
    // [JsonIgnore]
    // public string ConnectionString { get; set; }
    

}


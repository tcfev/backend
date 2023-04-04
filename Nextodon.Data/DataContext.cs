using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;

namespace Nextodon.Data;

public sealed class DataContext
{
    internal readonly IMongoDatabase database;
    public readonly IMongoCollection<Marker> Marker;
    public readonly IMongoCollection<Account> Account;
    public readonly IMongoCollection<Status> Status;
    public readonly IMongoCollection<Media> Media;
    public readonly IMongoCollection<Instance> Instance;
    public readonly IMongoCollection<Status_Account> StatusAccount;
    public readonly IMongoCollection<Relationship> Relationship;

    private static bool inited = false;

    static DataContext()
    {
        RegisterConventions();
    }

    public static void RegisterConventions()
    {
        if (inited)
        {
            return;
        }

        var pack = new ConventionPack
            {
                new IgnoreIfNullConvention(false),
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
            };

        ConventionRegistry.Register("camelCase", pack, t => true);
        inited = true;
    }

    public DataContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        database = client.GetDatabase(settings.Value.Database);

        Account = database.GetCollection<Account>("account");
        Instance = database.GetCollection<Instance>("instance");
        Marker = database.GetCollection<Marker>("marker");
        Media = database.GetCollection<Media>("media");
        Status = database.GetCollection<Status>("status");
        Relationship = database.GetCollection<Relationship>("relationship");

        StatusAccount = database.GetCollection<Status_Account>("status_account");
    }

    //public async Task VoteAsync(string statusId, string userId, List<VoteChoice> choices)
    //{
    //    var filter = Builders<Poll>.Filter.Eq(p => p.StatusId, statusId);

    //    var update = Builders<Poll>.Update.Set(p => p.Votes[userId], new Vote { UserId = userId, Choices = choices });
    //    var result = await Poll.UpdateOneAsync(filter, update);
    //}
}
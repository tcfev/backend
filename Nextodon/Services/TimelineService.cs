using Microsoft.EntityFrameworkCore;

namespace Nextodon.Services;

[Authorize]
[AllowAnonymous]
public sealed class TimelineService : Nextodon.Grpc.Timeline.TimelineBase
{
    private readonly MastodonContext db;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(ILogger<TimelineService> logger, MastodonContext db)
    {
        _logger = logger;
        this.db = db;
    }

    public override async Task<Grpc.Statuses> GetPublic(GetPublicTimelineRequest request, ServerCallContext context)
    {
        var account = await context.GetAccount(db, false);

        var local = request.Local;
        var remote = request.Remote;
        var onlyMedia = request.OnlyMedia;
        var sinceId = request.HasSinceId ? request.SinceId : null;
        var maxId = request.HasMaxId ? request.MaxId : null;
        var minId = request.HasMinId ? request.MinId : null;

        var limit = request.HasLimit ? request.Limit : 40;

        IQueryable<Data.PostgreSQL.Models.Status> query = db.Statuses.OrderByDescending(x => x.CreatedAt);


        if (!string.IsNullOrWhiteSpace(maxId))
        {
            //q = q.Where(x => x.Id < maxId);
        }

        if (!string.IsNullOrWhiteSpace(minId))
        {
            //q = q.Where(x => x.Id > minId);
        }

        query = query.Take((int)limit);

        var statuss = await query.ToListAsync();

        var v = new Grpc.Statuses();

        foreach (var status in statuss)
        {
            var s = await status.ToGrpc(account, db, context);
            v.Data.Add(s);
        }

        return v;
    }

    public override Task<Statuses> GetByTag(StringValue request, ServerCallContext context)
    {
        return base.GetByTag(request, context);
    }

    public override async Task<Statuses> GetHome(DefaultPaginationParameters request, ServerCallContext context)
    {
        var account = await context.GetAccount(db, true);

        var sinceId = request.HasSinceId ? request.SinceId : null;
        var maxId = request.HasMaxId ? request.MaxId : null;
        var minId = request.HasMinId ? request.MinId : null;

        var limit = request.HasLimit ? request.Limit : 40;

        IQueryable<Data.PostgreSQL.Models.Status> query = db.Statuses.OrderByDescending(x => x.CreatedAt);

        if (!string.IsNullOrWhiteSpace(maxId))
        {
            //q = q.Where(x => x.Id < maxId);
        }

        if (!string.IsNullOrWhiteSpace(minId))
        {
            //q = q.Where(x => x.Id > minId);
        }

        query = query.Take((int)limit);


        var statuss = await query.ToListAsync();

        var v = new Grpc.Statuses();

        foreach (var status in statuss)
        {
            var s = await status.ToGrpc(account, db, context);
            v.Data.Add(s);
        }

        return v;
    }

    public override Task<Statuses> GetList(StringValue request, ServerCallContext context)
    {
        return base.GetList(request, context);
    }

#pragma warning disable CS0809
    [Obsolete]
    public override Task<Statuses> GetDirect(DefaultPaginationParameters request, ServerCallContext context)
    {
        return base.GetDirect(request, context);
    }
#pragma warning restore CS0809
}

using Microsoft.EntityFrameworkCore;

namespace Nextodon.Services;

[Authorize]
public sealed class StatusApiService : Nextodon.Grpc.StatusApi.StatusApiBase
{
    private readonly ILogger<StatusApiService> _logger;
    private readonly Data.PostgreSQL.MastodonContext db;
    private readonly EventSource<Grpc.Status, long> _es;

    public StatusApiService(ILogger<StatusApiService> logger, EventSource<Grpc.Status, long> es, Data.PostgreSQL.MastodonContext db)
    {
        _logger = logger;
        _es = es;
        this.db = db;
    }

    [AllowAnonymous]
    public override async Task<Grpc.Status> GetStatus(UInt64Value request, ServerCallContext context)
    {
        var account = await context.GetAccount(db, false);
        var statusId = (long)request.Value;

        var ret = await db.Statuses.FindAsync(statusId);

        return await ret!.ToGrpc(account, db, context);
    }

    [AllowAnonymous]
    public override async Task<Accounts> GetRebloggedBy(GetRebloggedByRequest request, ServerCallContext context)
    {
        var me = await context.GetAccount(db, false);
        var cancellationToken = context.CancellationToken;
        var statusId = (long)request.StatusId;

        var q = from x in db.Statuses
                where x.ReblogOfId == statusId
                select x.Account;

        var accounts = await q.ToListAsync(cancellationToken: cancellationToken);

        var v = new Accounts();

        foreach (var account in accounts)
        {
            var temp = await account.ToGrpc(me, db, context);
            v.Data.Add(temp);
        }

        return v;
    }

    [AllowAnonymous]
    public override async Task<Accounts> GetFavouritedBy(GetFavouritedByRequest request, ServerCallContext context)
    {
        var me = await context.GetAccount(db, false);
        var cancellationToken = context.CancellationToken;

        var statusId = (long)request.StatusId;

        var q = from x in db.Favourites
                where x.StatusId == statusId
                select x.AccountId;

        var accountIds = await q.ToListAsync(cancellationToken: cancellationToken);
        var distinctIds = accountIds.Distinct().ToList();

        var v = new Accounts();

        foreach (var id in distinctIds)
        {
            var result = await db.Accounts.FindAsync(id);

            if (result != null)
            {
                var temp = await result.ToGrpc(me, db, context);
                v.Data.Add(temp);
            }
        }

        return v;
    }

    [AllowAnonymous]
    public override Task<Grpc.Context> GetContext(StringValue request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override async Task<Grpc.Status> CreateStatus(CreateStatusRequest request, ServerCallContext context)
    {
        var account = await context.GetAccount(db, true);
        var host = context.GetHost();
        var channel = _es[account!.Id];

        var now = DateTime.UtcNow;

        var conversation = new Data.PostgreSQL.Models.Conversation
        {
            CreatedAt = now,
            UpdatedAt = now,
        };

        await db.Conversations.AddAsync(conversation);
        await db.SaveChangesAsync();

        var status = new Data.PostgreSQL.Models.Status
        {
            AccountId = account.Id!,
            Text = request.Status,
            CreatedAt = now,
            UpdatedAt = now,
            ConversationId = conversation.Id,
            Visibility = 0,
            //OrderedMediaAttachmentIds = request.MediaIds?.ToList(),
            Sensitive = request.Sensitive,
            Language = request.HasLanguage ? request.Language : "en",
            SpoilerText = request.SpoilerText,
            Local = true,
            //InReplyToId = request.HasInReplyToId ? request.InReplyToId : null,
        };

        await db.Statuses.AddAsync(status);
        await db.SaveChangesAsync();

        status.Uri = $"https://{host}/users/{account.Username}/statuses/{status.Id}";
        await db.SaveChangesAsync();

        var result = await status.ToGrpc(account, db, context);
        await channel.Writer.WriteAsync(result, context.CancellationToken);

        return result;
    }

    public override async Task<Grpc.Status> DeleteStatus(UInt64Value request, ServerCallContext context)
    {
        var cancellationToken = context.CancellationToken;
        var me = await context.GetAccount(db, true);

        var statusId = (long)request.Value;

        var query = from x in db.Statuses
                    where x.Id == statusId && x.AccountId == me!.Id
                    select x;

        await query.ExecuteDeleteAsync(cancellationToken);

        return new Grpc.Status { Id = statusId.ToString() };
    }

    public override async Task<Grpc.Status> Favourite(UInt64Value request, ServerCallContext context)
    {
        var cancellationToken = context.CancellationToken;
        var me = await context.GetAccount(db, true);

        var statusId = (long)request.Value;

        await db.FavouriteStatusAsync(statusId, me!.Id, cancellationToken: cancellationToken);

        var result = await db.Statuses.FindAsync(new object[] { statusId }, cancellationToken: cancellationToken);
        var ret = await result!.ToGrpc(me, db, context);

        return ret;
    }

    public override async Task<Grpc.Status> Unfavourite(UInt64Value request, ServerCallContext context)
    {
        var me = await context.GetAccount(db, true);

        var cancellationToken = context.CancellationToken;
        var statusId = (long)request.Value;

        var status = await db.Statuses.FindAsync(new object[] { statusId }, cancellationToken);

        if (status == null)
        {
            throw new RpcException(new global::Grpc.Core.Status(StatusCode.NotFound, statusId.ToString()));
        }

        await db.UnfavouriteStatusAsync(statusId, me!.Id, cancellationToken);

        var v = await status.ToGrpc(me, db, context);

        return v;
    }

    public override async Task<Grpc.Status> Bookmark(UInt64Value request, ServerCallContext context)
    {
        var cancellationToken = context.CancellationToken;
        var me = await context.GetAccount(db, true);

        var statusId = (long)request.Value;

        await db.BookmarkStatusAsync(statusId, me!.Id, cancellationToken: cancellationToken);

        var result = await db.Statuses.FindAsync(new object[] { statusId }, cancellationToken: cancellationToken);
        var ret = await result!.ToGrpc(me, db, context);

        return ret;
    }

    public override async Task<Grpc.Status> Unbookmark(UInt64Value request, ServerCallContext context)
    {
        var me = await context.GetAccount(db, true);

        var cancellationToken = context.CancellationToken;
        var statusId = (long)request.Value;

        var status = await db.Statuses.FindAsync(new object[] { statusId }, cancellationToken);

        if (status == null)
        {
            throw new RpcException(new global::Grpc.Core.Status(StatusCode.NotFound, statusId.ToString()));
        }

        await db.UnbookmarkStatusAsync(statusId, me!.Id, cancellationToken);

        var v = await status.ToGrpc(me, db, context);

        return v;
    }

    public override Task<Grpc.Status> Mute(UInt64Value request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<Grpc.Status> Unmute(UInt64Value request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<Grpc.Status> Pin(UInt64Value request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<Grpc.Status> Unpin(UInt64Value request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<Grpc.Status> Reblog(ReblogRequest request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }

    public override Task<Grpc.Status> Unreblog(UInt64Value request, ServerCallContext context)
    {
        throw new NotImplementedException();
    }
}

﻿using System;
using System.Collections.Generic;

namespace Nextodon.Data.PostgreSQL.Models;

public partial class Marker
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string Timeline { get; set; } = null!;

    public long LastReadId { get; set; }

    public int LockVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? User { get; set; }
}

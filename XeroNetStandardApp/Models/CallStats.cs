// Models/CallStats.cs
using System;

namespace XeroNetStandardApp.Models
{
    public sealed class CallStats
    {
        public DateTimeOffset LastCallUtc { get; init; }
        public int RowsInserted { get; init; }
    }
}

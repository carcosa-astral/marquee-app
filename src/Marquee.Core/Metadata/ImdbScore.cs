namespace Marquee.Core.Metadata;

/// <summary>An IMDb average rating (1 to 10) and how many people voted.</summary>
public readonly record struct ImdbScore(double Rating, int Votes);

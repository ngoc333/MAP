namespace MAP.C.Contract.Context;

public sealed record ClientContext(
    string? ProgramId,
    string? IpAddress,
    string? UserName,
    string? LocalPath);

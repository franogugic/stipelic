namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>
/// What a contact import found (dry run) or did (real import). <see cref="Rows"/> data rows were read;
/// <see cref="Valid"/> of them hold a valid email, and those split into <see cref="New"/> + <see cref="Existing"/> +
/// <see cref="Unsubscribed"/> + <see cref="DuplicatesInFile"/> (repeats of an email earlier in the file).
/// <see cref="InvalidCount"/> = <see cref="Rows"/> − <see cref="Valid"/>; <see cref="Invalid"/> lists the first 100.
/// <see cref="RemainingContactLimit"/> is what the plan leaves after this import (for a dry run: what it would leave),
/// null for an unlimited plan; a negative value on a dry run means the import would be refused (all or nothing).
/// </summary>
public sealed record ContactImportSummaryDto(
    bool DryRun,
    int Rows,
    int Valid,
    int New,
    int Existing,
    int DuplicatesInFile,
    int Unsubscribed,
    int InvalidCount,
    List<ContactImportInvalidRowDto> Invalid,
    int? RemainingContactLimit);

/// <param name="Row">The row number as a spreadsheet shows it (the header row, if any, is row 1).</param>
/// <param name="Value">The cell as written (trimmed), at most 254 characters.</param>
/// <param name="Reason"><c>EMPTY</c> | <c>TOO_LONG</c> | <c>INVALID_EMAIL</c>.</param>
public sealed record ContactImportInvalidRowDto(int Row, string Value, string Reason);

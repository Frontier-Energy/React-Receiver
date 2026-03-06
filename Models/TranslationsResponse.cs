namespace React_Receiver.Models;

public sealed record TranslationsResponse(
    string LanguageName,
    TranslationAppResponse App
);

public sealed record TranslationAppResponse(
    string Title,
    string PoweredBy,
    string Brand
);

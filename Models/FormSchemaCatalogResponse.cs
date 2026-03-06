namespace React_Receiver.Models;

public sealed record FormSchemaCatalogResponse(
    FormSchemaCatalogItemResponse[] Items
);

public sealed record FormSchemaCatalogItemResponse(
    string FormType,
    string Version,
    string Etag
);

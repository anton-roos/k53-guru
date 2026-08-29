// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace K53Guru.Application.Features.Documents.Commands.Upload;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(v => v.UploadRequests).NotNull().NotEmpty();
    }
}

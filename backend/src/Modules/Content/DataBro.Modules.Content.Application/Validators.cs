using System.Text.RegularExpressions;
using FluentValidation;

namespace DataBro.Modules.Content.Application;

// Request-shape validation (docs/ERROR_HANDLING.md §3). Domain invariants (e.g. "publishable")
// remain in the Domain layer; these only guard the incoming payload shape.

internal static partial class SlugRules
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public static partial Regex Pattern();
}

public sealed class ContentBlockDtoValidator : AbstractValidator<ContentBlockDto>
{
    public ContentBlockDtoValidator()
    {
        RuleFor(b => b.Id).NotEmpty().WithMessage("Each block must have an id.");
        RuleFor(b => b.Type).NotEmpty().WithMessage("Each block must have a type.");
    }
}

public sealed class ContentDocumentDtoValidator : AbstractValidator<ContentDocumentDto>
{
    public ContentDocumentDtoValidator()
    {
        RuleFor(c => c.Version).GreaterThan(0);
        RuleForEach(c => c.Blocks).SetValidator(new ContentBlockDtoValidator());
        RuleFor(c => c.Blocks)
            .Must(HaveUniqueIds).WithMessage("Block ids must be unique.");
    }

    private static bool HaveUniqueIds(IReadOnlyList<ContentBlockDto> blocks)
    {
        var ids = blocks.Where(b => !string.IsNullOrWhiteSpace(b.Id)).Select(b => b.Id).ToList();
        return ids.Count == ids.Distinct().Count();
    }
}

public sealed class CreateArticleRequestValidator : AbstractValidator<CreateArticleRequest>
{
    public CreateArticleRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Summary).MaximumLength(1000);
        RuleFor(r => r.Locale).NotEmpty().MaximumLength(10);
        RuleFor(r => r.Content).NotNull().SetValidator(new ContentDocumentDtoValidator());
        RuleFor(r => r.Slug!)
            .Must(s => SlugRules.Pattern().IsMatch(s))
            .When(r => !string.IsNullOrWhiteSpace(r.Slug))
            .WithMessage("Slug must be lowercase letters, digits and hyphens (e.g. 'my-article').");
    }
}

public sealed class UpdateArticleRequestValidator : AbstractValidator<UpdateArticleRequest>
{
    public UpdateArticleRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().MaximumLength(300);
        RuleFor(r => r.Summary).MaximumLength(1000);
        RuleFor(r => r.Content).NotNull().SetValidator(new ContentDocumentDtoValidator());
    }
}

// ---- Taxonomy ----

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(1000);
        RuleFor(r => r.Slug!)
            .Must(s => SlugRules.Pattern().IsMatch(s))
            .When(r => !string.IsNullOrWhiteSpace(r.Slug))
            .WithMessage("Slug must be lowercase letters, digits and hyphens (e.g. 'machine-learning').");
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(1000);
    }
}

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Slug!)
            .Must(s => SlugRules.Pattern().IsMatch(s))
            .When(r => !string.IsNullOrWhiteSpace(r.Slug))
            .WithMessage("Slug must be lowercase letters, digits and hyphens (e.g. 'rag').");
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator() => RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
}

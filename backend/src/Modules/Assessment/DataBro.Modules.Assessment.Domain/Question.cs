using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Assessment.Domain;

/// <summary>
/// One question and its choices. Not an aggregate root — a question only means anything inside its
/// quiz, and reordering is what an authoring UI does constantly.
/// </summary>
public sealed class Question : Entity
{
    private readonly List<Choice> _choices = [];

    public Guid QuizId { get; private set; }
    public string Prompt { get; private set; } = string.Empty;
    public QuestionType Type { get; private set; }
    public int Order { get; private set; }

    /// <summary>
    /// Marks this question is worth. Whole marks only, and <b>all or nothing</b> — see
    /// <see cref="Score"/> for why partial credit is refused rather than approximated.
    /// </summary>
    public int Points { get; private set; }

    /// <summary>Author-written note shown after an attempt is submitted, never before.</summary>
    public string? Explanation { get; private set; }

    public IReadOnlyList<Choice> Choices => _choices.OrderBy(c => c.Order).ToList();

    private Question() { } // EF

    internal Question(Guid id, Guid quizId, string prompt, QuestionType type, int order, int points)
        : base(id)
    {
        QuizId = quizId;
        Prompt = prompt.Trim();
        Type = type;
        Order = order;
        Points = Math.Max(1, points);

        // True/false is a single-choice question with its options written for it. Generating them
        // here means an author cannot create a true/false question with four answers, and every
        // scoring path treats it as the single-choice question it is.
        if (type == QuestionType.TrueFalse)
        {
            _choices.Add(new Choice(Guid.NewGuid(), id, "True", false, 0));
            _choices.Add(new Choice(Guid.NewGuid(), id, "False", false, 1));
        }
    }

    internal void SetOrder(int order) => Order = order;

    public void Describe(string prompt, int points, string? explanation = null)
    {
        Prompt = prompt.Trim();
        Points = Math.Max(1, points);
        Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim();
    }

    public Result AddChoice(Guid choiceId, string text, bool isCorrect)
    {
        if (Type == QuestionType.TrueFalse)
            return Result.Failure(Error.Rule("A true/false question already has its two choices."));

        _choices.Add(new Choice(choiceId, Id, text, isCorrect, _choices.Count));
        NormaliseChoices();

        return Result.Success();
    }

    public Result RemoveChoice(Guid choiceId)
    {
        if (Type == QuestionType.TrueFalse)
            return Result.Failure(Error.Rule("A true/false question's choices cannot be removed."));

        var choice = _choices.FirstOrDefault(c => c.Id == choiceId);
        if (choice is null) return Result.Failure(Error.NotFound("Choice not found in this question."));

        _choices.Remove(choice);
        NormaliseChoices();

        return Result.Success();
    }

    /// <summary>
    /// Replaces the answer key.
    ///
    /// <para>
    /// Set as a whole rather than toggled per choice: for a single-choice question the correct set
    /// has exactly one member, and a per-choice toggle makes "two correct answers on a single-choice
    /// question" a state the author has to pass through. Here it cannot be reached at all.
    /// </para>
    /// </summary>
    public Result SetCorrectChoices(IReadOnlyCollection<Guid> correctChoiceIds)
    {
        if (correctChoiceIds.Count == 0)
            return Result.Failure(Error.Validation("A question needs at least one correct choice."));

        if (correctChoiceIds.Any(id => _choices.All(c => c.Id != id)))
            return Result.Failure(Error.Validation("The answer refers to a choice that is not in this question."));

        if (Type is QuestionType.SingleChoice or QuestionType.TrueFalse && correctChoiceIds.Count > 1)
            return Result.Failure(Error.Validation("A single-choice question has exactly one correct choice."));

        foreach (var choice in _choices) choice.SetCorrect(correctChoiceIds.Contains(choice.Id));

        return Result.Success();
    }

    /// <summary>
    /// Whether this question can go live. A question with no correct answer, or with fewer than two
    /// choices, cannot be answered correctly by anyone — publishing it would be shipping a trap.
    /// </summary>
    public Result ValidateForPublish()
    {
        if (string.IsNullOrWhiteSpace(Prompt))
            return Result.Failure(Error.Rule($"A question in this quiz has no prompt."));

        if (_choices.Count < 2)
            return Result.Failure(Error.Rule($"'{Prompt}' needs at least two choices."));

        if (_choices.All(c => !c.IsCorrect))
            return Result.Failure(Error.Rule($"'{Prompt}' has no correct answer."));

        return Result.Success();
    }

    /// <summary>
    /// Marks awarded for a set of selected choices.
    ///
    /// <para>
    /// <b>All or nothing</b>, including for multiple-choice: the selection must be exactly the
    /// correct set — every right choice and no wrong ones. Partial credit sounds kinder and is
    /// arbitrary; there is no defensible number for "two of three right, one wrong", and every
    /// scheme that invents one rewards guessing broadly. A learner who wants partial credit is
    /// better served by the author splitting the question.
    /// </para>
    /// </summary>
    public int Score(IReadOnlyCollection<Guid> selectedChoiceIds)
    {
        var correct = _choices.Where(c => c.IsCorrect).Select(c => c.Id).ToHashSet();
        var selected = selectedChoiceIds.ToHashSet();

        return correct.SetEquals(selected) ? Points : 0;
    }

    private void NormaliseChoices()
    {
        var ordered = _choices.OrderBy(c => c.Order).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].SetOrder(i);
    }
}

/// <summary>
/// One selectable answer.
///
/// <para>
/// <see cref="IsCorrect"/> is the answer key. It exists on the entity because scoring needs it, and
/// it must never appear in a learner-facing DTO — a quiz that ships its own answers is still a
/// working quiz, which is exactly why the leak would go unnoticed.
/// </para>
/// </summary>
public sealed class Choice : Entity
{
    public Guid QuestionId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
    public int Order { get; private set; }

    private Choice() { } // EF

    internal Choice(Guid id, Guid questionId, string text, bool isCorrect, int order) : base(id)
    {
        QuestionId = questionId;
        Text = text.Trim();
        IsCorrect = isCorrect;
        Order = order;
    }

    internal void SetCorrect(bool isCorrect) => IsCorrect = isCorrect;
    internal void SetOrder(int order) => Order = order;

    public void Rename(string text) => Text = text.Trim();
}

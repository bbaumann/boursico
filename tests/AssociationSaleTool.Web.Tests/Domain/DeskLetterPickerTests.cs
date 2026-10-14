using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class DeskLetterPickerTests
{
    private const long Desk = 1;
    private const long OtherDesk = 2;

    private static List<LetterState> Letters(params (char Letter, int Used, long? Holder)[] overrides)
    {
        var letters = DeskLetterPicker.SafeAlphabet
            .Select(letter => new LetterState(letter, 0, null))
            .ToList();
        foreach (var (letter, used, holder) in overrides)
        {
            var index = letters.FindIndex(x => x.Letter == letter);
            letters[index] = new LetterState(letter, used, holder);
        }

        return letters;
    }

    [Fact]
    public void SafeAlphabet_ExcludesTheReservedAndConfusableLetters()
    {
        Assert.Equal("BCDEGHJKLMNPRSTUWXYZ", string.Concat(DeskLetterPicker.SafeAlphabet));
    }

    [Fact]
    public void AlphabetIndex_FollowsTheAlphabetOrderAndSortsUnknownLettersLast()
    {
        Assert.Equal(0, DeskLetterPicker.AlphabetIndex('B'));
        Assert.Equal(4, DeskLetterPicker.AlphabetIndex('G'));
        Assert.Equal(19, DeskLetterPicker.AlphabetIndex('Z'));
        Assert.Equal(int.MaxValue, DeskLetterPicker.AlphabetIndex('A'));
    }

    [Fact]
    public void PickFreeLetters_OnAnEmptyEventTakesTheFirstLettersOfTheAlphabet()
    {
        Assert.Equal(['B', 'C'], DeskLetterPicker.PickFreeLetters(Letters(), 2, 455));
    }

    [Fact]
    public void PickFreeLetters_SkipsHeldAndExhaustedLettersAndPrefersTheLeastUsed()
    {
        var letters = Letters(('B', 0, OtherDesk), ('C', 455, null), ('D', 40, null), ('E', 10, null));

        // G..Z are unused, so they come before D (40) and E (10).
        Assert.Equal(['G', 'H'], DeskLetterPicker.PickFreeLetters(letters, 2, 455));
    }

    [Fact]
    public void PickFreeLetters_ReturnsNothingForAZeroCount()
    {
        Assert.Empty(DeskLetterPicker.PickFreeLetters(Letters(), 0, 455));
    }

    [Fact]
    public void PlanRun_KeepsTheSellerLetterWhenItFits()
    {
        var letters = Letters(('B', 12, Desk), ('C', 0, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, 'B', 1, 455);

        Assert.NotNull(plan);
        Assert.Equal([new LetterSegment('B', 1)], plan.Segments);
        Assert.Empty(plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_WithoutSellerLetterUsesTheHeldLetterWithMostRoom()
    {
        var letters = Letters(('B', 30, Desk), ('C', 10, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, null, 12, 455);

        Assert.Equal([new LetterSegment('C', 12)], plan!.Segments);
    }

    [Fact]
    public void PlanRun_IgnoresTheSellerLetterWhenAnotherDeskHoldsIt()
    {
        var letters = Letters(('B', 12, OtherDesk), ('K', 5, Desk), ('R', 50, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, 'B', 1, 455);

        Assert.Equal([new LetterSegment('K', 1)], plan!.Segments);
    }

    [Fact]
    public void PlanRun_MovesAWholeRunToTheOtherLetterRatherThanSplitting()
    {
        // Spec example 4: B has 2 left, C has room for the 5 rows.
        var letters = Letters(('B', 18, Desk), ('C', 3, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, null, 5, 20);

        Assert.Equal([new LetterSegment('C', 5)], plan!.Segments);
    }

    [Fact]
    public void PlanRun_SplitsAcrossHeldLettersThenTakesAFreeLetter()
    {
        var letters = Letters(('B', 18, Desk), ('C', 17, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, 'B', 8, 20);

        Assert.Equal(
            [new LetterSegment('B', 2), new LetterSegment('C', 3), new LetterSegment('D', 3)],
            plan!.Segments);
        Assert.Equal(['D'], plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_WithoutSellerLetterSplitsMostRoomFirstAmongHeldLetters()
    {
        var letters = Letters(('B', 18, Desk), ('C', 17, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, null, 6, 20);

        Assert.Equal(
            [new LetterSegment('C', 3), new LetterSegment('B', 2), new LetterSegment('D', 1)],
            plan!.Segments);
        Assert.Equal(['D'], plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_TakesAFreeLetterWhenTheDeskHoldsNone()
    {
        var plan = DeskLetterPicker.PlanRun(Letters(), Desk, null, 3, 455);

        Assert.Equal([new LetterSegment('B', 3)], plan!.Segments);
        Assert.Equal(['B'], plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_ReturnsNullWhenThereIsNoRoomAnywhere()
    {
        var letters = DeskLetterPicker.SafeAlphabet
            .Select(letter => new LetterState(letter, 20, letter == 'B' ? Desk : null))
            .ToList();

        Assert.Null(DeskLetterPicker.PlanRun(letters, Desk, null, 1, 20));
    }

    [Fact]
    public void Room_IsNeverNegativeAfterTheSettingWasLowered()
    {
        Assert.Equal(0, DeskLetterPicker.Room(new LetterState('B', 30, null), 20));
    }
}

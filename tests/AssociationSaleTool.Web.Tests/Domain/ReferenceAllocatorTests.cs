using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class ReferenceAllocatorTests
{
    private const int Cap = 455;

    private static Dictionary<char, int> Empty() =>
        ReferenceAllocator.SafeAlphabet.ToDictionary(letter => letter, _ => 0);

    [Fact]
    public void ChooseLetter_UsesOwnLetterWhenItFits()
    {
        var used = Empty();
        used['K'] = 100;

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_UsesOwnLetterWhenItExactlyFits()
    {
        var used = Empty();
        used['K'] = 435;

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_SpillsToLeastLoadedLetterWhenOwnLetterIsFull()
    {
        var used = Empty();
        used['K'] = 450;
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 400;
        }

        used['K'] = 450;
        used['S'] = 10;
        used['T'] = 50;

        Assert.Equal('S', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_BreaksSpillTiesByAlphabetOrder()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 400;
        }

        used['K'] = 450;
        used['T'] = 10;
        used['S'] = 10;

        Assert.Equal('S', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_PrefersTheSellerExistingLetterOnTopUp()
    {
        var used = Empty();
        used['K'] = 100;
        used['M'] = 200;

        Assert.Equal('M', ReferenceAllocator.ChooseLetter(used, 'K', 'M', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_FallsBackToOwnLetterWhenExistingLetterIsFull()
    {
        var used = Empty();
        used['K'] = 100;
        used['M'] = 454;

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', 'M', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_RelaxesIntoOwnLetterWhenEveryLetterIsFull()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_RelaxesIntoOwnLetterEvenWhenTheSellerHasAnExistingLetter()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', 'M', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_ThrowsWhenAllocationWouldPassTheHardCap()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        used['K'] = 998;

        Assert.Throws<ReferenceSpaceExhaustedException>(
            () => ReferenceAllocator.ChooseLetter(used, 'K', null, 5, Cap));
    }

    [Fact]
    public void SafeAlphabet_MatchesTheSellerCodeAlphabet()
    {
        Assert.Equal(20, ReferenceAllocator.SafeAlphabet.Count);
        Assert.DoesNotContain('A', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('I', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('O', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('Q', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('V', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('F', ReferenceAllocator.SafeAlphabet);
    }
}

using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Domain.Sellers;

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

    [Fact]
    public void SafeAlphabet_MatchesTheSellerCodeAlphabetInOrder()
    {
        // The bucket-balance argument in ReferenceAllocator's spill tie-break
        // depends on SafeAlphabet sharing SellerCodeGenerator's exact order,
        // not merely its membership - a count-and-exclusions check would stay
        // green even if the two lists were reordered relative to each other.
        Assert.Equal(
            ReferenceAllocator.SafeAlphabet,
            Enumerable.Range(0, 20).Select(i => SellerCodeGenerator.GetCodeAt(i)[0]));
    }

    [Fact]
    public void ChooseLetter_IgnoresAStaleExistingLetterThatIsNotInTheSafeAlphabet()
    {
        var used = Empty();
        used['K'] = 100;

        // 'A' is reserved / outside SafeAlphabet, as a stale pre-migration
        // reference like "AA-001" would produce. The top-up tier must not
        // trust it and should fall through to the seller's own letter.
        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', 'A', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_IgnoresAnOwnLetterThatIsNotInTheSafeAlphabet()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 400;
        }

        used['S'] = 10;
        used['T'] = 50;

        // ownLetter 'A' cannot come from a well-formed seller code, but a
        // hand-edited database could still produce it. It must not be
        // trusted either, so allocation spills to the least-loaded letter.
        Assert.Equal('S', ReferenceAllocator.ChooseLetter(used, 'A', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_RelaxesToTheLeastLoadedLetterWhenOwnLetterIsNotInTheSafeAlphabetAndEveryLetterIsFull()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        used['S'] = 450;

        // Every letter is full and ownLetter is invalid, so relaxing past
        // the soft cap must still land on a real bucket rather than the
        // stale letter.
        Assert.Equal('S', ReferenceAllocator.ChooseLetter(used, 'A', null, 5, Cap));
    }
}

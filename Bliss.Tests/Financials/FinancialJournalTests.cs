using Bliss.Domain.Financials;
namespace Bliss.Tests.Financials;
public sealed class FinancialJournalTests
{
    private static readonly FinancialScope Scope = new(Guid.NewGuid(), FinancialModule.BlissBot, Guid.NewGuid());
    private static JournalLine[] Lines(long amount = 100) => [new(Guid.NewGuid(), Guid.NewGuid(), "advertiser", JournalSide.Debit, amount),new(Guid.NewGuid(), Guid.NewGuid(), "creator", JournalSide.Credit, amount)];
    [Fact] public void Unbalanced_or_nonpositive_money_is_refused()
    { var lines=Lines();lines[1]=lines[1] with { MinorUnits=99 };Assert.Throws<ArgumentException>(()=>new FinancialJournal(Scope,"USD","a","quote1",lines));Assert.Throws<ArgumentException>(()=>new FinancialJournal(Scope,"USD","b","quote1",Lines(0))); }
    [Fact] public void Overflow_is_refused() {var l=Lines(long.MaxValue);Assert.Throws<OverflowException>(()=>new FinancialJournal(Scope,"USD","a","q",l.Concat(l)));}
    [Theory][InlineData("usd")][InlineData("XYZ")][InlineData(" USD")] public void Unknown_or_noncanonical_currency_is_refused(string currency)=>Assert.Throws<ArgumentException>(()=>new FinancialJournal(Scope,currency,"a","q",Lines()));
    [Fact] public void Input_array_mutation_does_not_change_evidence() {var l=Lines();var j=new FinancialJournal(Scope,"USD","a","q",l);l[0]=l[0] with {MinorUnits=1};Assert.Equal(100,j.Lines[0].MinorUnits);}
    [Fact] public void Reversal_preserves_scope_and_attribution() {var j=new FinancialJournal(Scope,"USD","a","q",Lines());var r=j.Reverse("r","refund");Assert.Equal(j.Id,r.ReversesId);Assert.Equal(j.Scope,r.Scope);Assert.Equal(j.Lines[0].OwnerId,r.Lines[0].OwnerId);Assert.Equal(JournalSide.Credit,r.Lines[0].Side);}
    [Fact] public void Missing_trusted_scope_is_refused()=>Assert.Throws<ArgumentException>(()=>new FinancialJournal(Scope with {MerchantId=Guid.Empty},"USD","a","q",Lines()));
    [Fact] public void Fingerprint_detects_payee_and_evidence_changes() {var l=Lines();var a=new FinancialJournal(Scope,"USD","a","q",l);Assert.Equal(a.Fingerprint,new FinancialJournal(Scope,"USD","a","q",l).Fingerprint);l[1]=l[1] with {OwnerId=Guid.NewGuid()};Assert.NotEqual(a.Fingerprint,new FinancialJournal(Scope,"USD","a","q",l).Fingerprint);}
}

# Prospect demonstration example — ABC Pharmacy, Panama City

**Status:** reference picture of the finished prospect-facing artifact.
Not an implementation authorization. ABC Pharmacy in this example is a
label for the sales room, not a claim that a business by that name has
asked for advertising.

The image is `examples/abc-pharmacy-panama-city.png`.

## Who does what

The person who supplies media does not research the prospect, design
the overlay, build the page, or send the message.

| Work | Who |
| --- | --- |
| Choose podcast or similar video from different cities, cultures, and countries | Human source-media producer |
| Cut about 15 seconds and upload the slice to the Source Media Library | Human source-media producer |
| Find a real local business and the person who can buy advertising | Bots: Fishing Fleet, then Decision-Maker and Contact Intelligence |
| Produce the overlay advertisement and composite it onto the uploaded slice | Bots: creative workers for the overlay, deterministic software for the composite, QR, and disclosure |
| Build one private landing page that plays those videos | Bots: Dynamic Prospect Sales Room |
| Send the page to the business | Bots: Communication Router, after outreach eligibility |
| Continue the conversation with the potential decision-maker | Bots: AI Business Development, with human escalation for closing |

Variety across cities, cultures, and countries is the point of the
library. A slice still has to be a source Alpha is allowed to use, with
provenance recorded. An unapproved copy of someone else's podcast is
not qualified media and does not earn quota credit.

## The business the bots find

The bots research a real established local or regional business. The
reference uses a Panama City pharmacy and presents it as ABC Pharmacy.
The same path applies to a restaurant, car dealer, coffee shop, real
estate office, law firm, grocery store, and the other profiles in
`MASTER-ARCHITECTURE.md`.

Use the verified public name when confidence supports it. Do not
invent a business, a person, or a contact to fill the page. If the
decision-maker is unverified, the page and the message stay on the
business and the verified contact route. They do not address a made-up
person.

## What the recipient receives

One message, then one private page. The reference message is an email.
The same facts must be able to travel through the other approved
channels. The conversation stays one ledger entry for that prospect.

### Message

- Brand: Alpha Podcast Advertising.
- Line: More Voices. More Reach. More Customers.
- Subject shape: see how {business} could reach more customers in {market}.
- The reference subject is "See how ABC Pharmacy could reach more customers in Panama City."
- Body: Alpha helps local businesses get exposure by placing ads inside podcasts and video that people in that community already watch and listen to.
- The clips are private customized concepts created for that business, not a claim of an existing campaign.
- Benefit lines in the reference: reach more local customers; be part of popular podcasts in the market; build awareness in the community; drive customers to the store, website, or offers.
- One button: View Your Private Demonstration.
- Invitation to reply. The bot continues that conversation.
- Sign-off: The Alpha Team, Alpha Podcast Advertising.
- Footer: this is a private demonstration created for that business for illustrative purposes only.

### Landing page

One application. The prospect id selects the facts. The reference page
is the shape:

- Brand: Alpha Podcast Advertising.
- Context navigation: market, business type, and audience. The reference uses Panama City, Local Business, and Real Audiences.
- Prospect name as the title. The reference title is ABC Pharmacy.
- Promise: get more customers in that market with podcast advertising.
- Short line that the community is listening and the brand can be placed in front of them with professional podcast advertising.
- A local-results mark. The reference says "Local People. Real Results."
- Concept cards. Each card is one playable video of about 15 seconds: the uploaded slice with the bot-produced overlay, a concept name, advertising copy in the market language, a call to action, and a real QR code.
- The reference cards are Family Health ("Tu Salud Nuestra Prioridad"), Convenience ("Todo lo que necesitas en un solo lugar"), Wellness ("Vive Bien Todos los Días"), and Neighborhood ("Tu Farmacia de Confianza en Panama City").
- A benefit row: local audience reach, engaging ad formats, measurable results, and a trusted partner.
- Closing brand line in the reference: More Voices. More Value.

The page chrome in the reference is English. The overlays are Spanish,
which is the right market language for this Panama City example.
Language is data. A Manila page does not copy this mix.

## Disclosure

The reference page states that it is a private advertising
demonstration created by Alpha for illustrative business-development
purposes, that the named business has not sponsored, commissioned,
approved, or endorsed the concepts, that example offers, products, and
promotions are illustrative unless identified as verified business
information, and that no commercial relationship is implied.

That disclosure is an automated production component. It is not optional
copy for the person who uploaded the slice.

## How many videos

The reference page has four concept slots so the product shape is
clear. Each slot is an approved slice plus an overlay, not a separately
built website and not a generative-video reshoot.

Compositing the overlay, the QR, and the disclosure is software.
Generating the overlay creative is the AI spend. A cold prospect does
not automatically receive four expensive overlay generations. The first
send can be one strong concept on this page. Further cards are added
when engagement or opportunity value justifies them, under the budget
policy. Empty slots are not filled with fabricated ads.

## What this example does not authorize

It does not authorize downloading arbitrary podcasts, emailing a
business before contact verification and outreach eligibility, inventing
ABC Pharmacy's staff, or starting the factory before Bliss reaches its
required acceptance point. The contracts that eventually implement this
picture are still the unopened items in `FUTURE-CONTRACTS.md`.

A bounded operator screen can record a business name only with a public
source URL and can prepare one demonstration after the opportunity
score passes. See `ADVERTISER-DISCOVERY-EVIDENCE.md`. That screen is not
a crawler and it does not send the message.

A later screen can record a person or a company contact only from a
public source. Confidence and the contact tier follow that evidence.
A stale record is not treated as current, and delivery stays unsent.
See `DECISION-MAKER-EVIDENCE.md`.

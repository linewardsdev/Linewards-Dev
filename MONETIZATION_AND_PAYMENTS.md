# Monetization And Payments

## Purpose

Define a fair, store-compliant monetization plan for Line Tower Wars (LTW), including which payment provider handles each kind of purchase and what is intentionally deferred from the offline MVP.

## Product Rule

LTW is **cosmetic-only**. A purchase must never grant combat strength, towers, creeps, income, lives, cooldown changes, matchmaking advantage, or access to gameplay rules.

Allowed examples:

- Tower skins and projectile effects.
- Lane themes and builder cosmetics.
- Player badges, profile banners, emotes, and cosmetic mass-send alerts.
- Cosmetic seasonal passes, if every reward remains cosmetic.

Not allowed:

- Paid gameplay units, stats, resources, queues, upgrades, or discounts.
- Loot boxes with paid random gameplay value.
- Ads that grant gameplay resources or combat advantages.

## Payment Provider Decision

| Where the customer buys | Product type | Required payment rail | LTW decision |
| --- | --- | --- | --- |
| iOS/iPadOS app | Cosmetic digital item | Apple In-App Purchase / StoreKit | Use Apple’s store checkout. |
| Android app distributed through Google Play | Cosmetic digital item | Google Play Billing | Use Google’s store checkout. |
| Website | Physical merchandise or other non-digital goods | A web processor, such as Stripe | Optional future use; outside the game client. |
| Website or game client | Game cosmetics, virtual currency, or premium digital access | Do not introduce a card processor as the default payment route | Defer. Store rules and regional exceptions require separate legal/product review. |

Apple requires In-App Purchase when an app unlocks in-app features or functionality, including in-game currency and premium content. [Apple App Review Guidelines 3.1.1](https://developer.apple.com/app-store/review/guidelines/)

Google Play requires Google Play Billing for in-app purchases of digital goods and services distributed through Google Play. [Google Play Payments policy](https://support.google.com/googleplay/android-developer/answer/10281818)

**Practical outcome:** do not collect card numbers, billing addresses, or payment credentials in LTW. Apple and Google handle checkout, payment methods, tax collection where applicable, and refund initiation through their stores.

## MVP Decision

The offline MVP ships with **no live monetization**:

- No store screen that accepts payment.
- No payment SDK, Stripe SDK, or direct card form.
- No virtual currency sold for money.
- No customer account or cloud entitlement system.

Cosmetic catalogue UI may be prototyped with clearly labelled non-purchasable placeholders, but it must not imply that a real purchase is available until platform products, pricing, policy text, and entitlement handling are ready.

## Post-MVP Store Architecture

```text
Player taps a cosmetic offer
        |
        +--> iOS: StoreKit / Apple In-App Purchase
        |
        +--> Android: Google Play Billing
        |
        v
Platform returns transaction result
        |
        v
Validate transaction and grant cosmetic entitlement
        |
        v
Persist entitlement and refresh the cosmetic inventory
```

### Client Responsibilities

- Display the platform-provided localized price and product information.
- Start the platform purchase flow only after the player explicitly confirms.
- Restore previously purchased non-consumable cosmetics where the platform supports restoration.
- Treat a purchase as pending until the transaction is confirmed.
- Provide a clear restore-purchases action and a support path.

### Future Server Responsibilities

When LTW adds accounts or cross-device cosmetics, a trusted service should validate store transactions and own the durable entitlement record. The client must not be trusted to mint cosmetics from a local purchase-success flag.

This service is deliberately deferred until the local MVP is proven. Apple offers transaction-status and notification APIs for this later stage; Google provides corresponding purchase-verification flows.

## Product Catalogue

Use stable, platform-neutral internal IDs. Map them to Apple and Google product IDs at release time.

| Internal cosmetic ID | Type | Suggested store product type | Notes |
| --- | --- | --- | --- |
| `cosmetic.tower.neon` | Tower skin | Non-consumable | Permanent cosmetic unlock. |
| `cosmetic.lane.volcanic` | Lane theme | Non-consumable | Permanent cosmetic unlock. |
| `cosmetic.emote.gg` | Emote | Non-consumable | No gameplay communication advantage. |
| `cosmetic.pass.season-01` | Cosmetic pass | Non-consumable or subscription only after review | Do not ship until rewards, expiry, and regional policy are defined. |

Avoid a paid currency in the first store release. Direct cosmetic purchases are easier to explain, test, refund, and reconcile.

## Pricing And Store Readiness Checklist

Before enabling any real product:

- [ ] Confirm every item is cosmetic-only and review it against the project’s fair-play rule.
- [ ] Create matching products in App Store Connect and Google Play Console.
- [ ] Choose local price tiers, display names, descriptions, and support contact details.
- [ ] Add screenshots and clear purchase disclosures required by each store.
- [ ] Implement purchase, pending, cancelled, failed, restored, and refunded states.
- [ ] Test using Apple sandbox/TestFlight and Google Play test tracks; never use production transactions as the primary test environment.
- [ ] Define customer-support and refund guidance that directs platform-store refund requests appropriately.
- [ ] Complete privacy, tax, consumer-protection, age-rating, and regional-policy review before launch.
- [ ] Add server-side receipt/purchase validation before enabling cross-device ownership or high-value cosmetics.

## Provider Evaluation Notes

### Apple

- Use StoreKit and App Store Connect products for digital cosmetics in the iOS app.
- TestFlight purchases use Apple’s sandbox environment and do not charge testers. [Apple In-App Purchase testing](https://developer.apple.com/in-app-purchase/)
- The paid Apple Developer Program is needed for TestFlight distribution; it is not needed to keep building the no-payment MVP.

### Google Play

- Use Google Play Billing for digital cosmetics in Google Play builds.
- Use Play Console license testers and test tracks before production.
- Review current service-fee programs and regional rules when pricing is chosen; they change over time. [Google Play service fees](https://support.google.com/googleplay/android-developer/answer/112622)

### Stripe Or Another Web Processor

- Appropriate for a future LTW website selling physical merchandise.
- Not selected for in-app cosmetic checkout on iOS or Google Play Android builds.
- If a web store ever sells digital cosmetics, obtain product, legal, tax, and platform-policy review first; regional rules differ and evolve.

## Ownership And Review

| Area | Owner before launch |
| --- | --- |
| Cosmetic design and fair-play review | Product/design owner |
| Apple product configuration | Apple account holder |
| Google product configuration | Play Console owner |
| Purchase implementation | Client engineer |
| Receipt validation and cross-device entitlements | Backend owner, when introduced |
| Tax, terms, privacy, refunds, and regional compliance | Business/legal owner |

## Explicit Deferrals

- Ads and rewarded ads.
- Subscriptions.
- Paid virtual currency.
- Loot boxes or randomized monetization.
- Web checkout for in-app digital items.
- Third-party payment SDKs in the mobile client.
- Account-linked entitlements and a payment backend.

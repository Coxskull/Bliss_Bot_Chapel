# PHASE-1-TRACEABILITY

| Requirement ID | Description | Entity/Component | Test | Status |
| --- | --- | --- | --- | --- |
| BLC-ARCH-001 | Creator supports many ContentItems. | `Creator`, `ContentItem` | `Creator_supports_multiple_content_items` | Implemented |
| BLC-ARCH-002 | ContentItem supports many AdInventorySlots. | `ContentItem`, `AdInventorySlot` | `Content_item_supports_multiple_ad_inventory_slots` | Implemented |
| BLC-ARCH-003 | Creator supports many simultaneous BlissMatches. | `BlissMatch` | `Creator_supports_multiple_simultaneous_bliss_matches_without_uniqueness_violation`; `Bliss_match_creator_id_is_not_unique` | Implemented |
| BLC-ARCH-004 | A new BlissMatch does not overwrite another valid BlissMatch. | `BlissMatch` | `New_bliss_match_does_not_overwrite_another_valid_match` | Implemented |
| BLC-ADV-001 | Advertiser ≠ Program ≠ Opportunity. | `Advertiser`, `AdvertiserProgram`, `AdvertiserOpportunity` | Seed graph + separate tables/FKs | Implemented |
| BLC-ACCESS-001 | Network access and Program approval are persisted separately. | `NetworkAccess`, `ProgramAccess` | `Network_access_and_program_access_are_independent` | Implemented |
| BLC-DATA-001 | Demographic data preserves source, confidence, timestamp. | `DataProvenance` | `Data_provenance_retains_source_confidence_and_collected_at` | Implemented |
| BLC-DATA-002 | UNKNOWN must not be treated as zero. | `Creator.FemalePercentage` | `Unknown_female_percentage_remains_null_and_is_not_converted_to_zero`; `Female_percentage_is_nullable` | Implemented |
| BLC-HIST-001 | Historical matches preserve Rule Version. | `BlissMatch.RuleVersionId` | `Historical_bliss_match_preserves_original_rule_version` | Implemented |
| BLC-HIST-002 | Future rules do not rewrite historical matches. | `RuleVersion` + Restrict FK | Same historical test; delete restrict on RuleVersion | Implemented |
| BLC-MULTI-001 | No ContentItem one-advertiser-only relationship. | `ContentItem` model | `There_is_no_direct_creator_to_advertiser_foreign_key` (includes ContentItem) | Implemented |
| BLC-MULTI-002 | Multiple CampaignPlacements per ContentItem. | `CampaignPlacement` | `Multiple_campaign_placements_can_reference_the_same_content_item`; `Campaign_placement_content_item_id_is_not_unique` | Implemented |
| BLC-PLACE-001 | Match compatibility is separate from placement eligibility. | `BlissMatch` vs `CampaignPlacement` / `EligibilityCheck` | Separate tables; eligibility rows are not placements | Implemented |
| BLC-PROV-001 | Normalized intelligence retains provenance. | `DataProvenance` | Provenance persistence test | Implemented |
| BLC-AI-001 | AI is not the authority for match arithmetic. | No scoring engine; nullable `MatchScoreComponent.Score` | Architecture review + absence of scoring services | Implemented |

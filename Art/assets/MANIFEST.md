# BLOOD & BEAN Generated Asset Manifest

| asset | temp_path |
|---|---|
| player_idle | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-71249b52-5035-438e-acec-a36ec863ef12.png |
| player_carry | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-f5a7d0f3-3464-4776-888e-32d456bd106f.png |
| guest_a | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-edc16e85-4d77-4871-9aa8-7f6423eb354a.png |
| guest_b | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-961ee6c0-f0e0-4d7c-bdc1-7796f4a4a9b9.png |
| guest_c | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-d662be36-e0cd-465c-a9cd-7eb6798643d9.png |
| counter | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-59608389-9966-43bb-8948-2512047388ac.png |
| grinder | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-467dde80-a687-47d9-9edc-ad68c5d593d7.png |
| espresso_machine | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-f88edf72-d75d-4ec4-b670-724c6fb13ddc.png |
| serving_station | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-62481f9c-b04e-4bb9-b7bb-65eff9145dea.png |
| bean_shelf | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-3024605c-2923-4f40-b3f5-ced0026a3b66.png |
| cafe_table | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-36655907-9a04-4478-aa2c-a8088e29a4b7.png |
| metal_container | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-67308e9f-68c5-4dbb-ba2e-abe506a4f064.png |
| drawer_chest | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-84ed1e4d-490a-4c1a-addb-73c79f365812.png |
| random_box | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-ca299eb3-257e-4d6c-ac9d-3a46a2e216af.png |
| tile_cafe_floor | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-8fa5f73a-2852-4830-9735-331ce8f623b2.png |
| tile_zone_floor | C:\Users\hope\.codex\generated_images\01a01e3d-58c0-7c23-9ccf-74560869ef1f\exec-c2957c08-e0c7-47f2-941e-f5ba0df97020.png |

## Visual QA

- All 18 entries passed visual inspection: background appears uniformly flat magenta and no magenta/pink/purple is visibly present inside any asset.
- Visual inspection only; exact per-pixel values were not programmatically sampled.

## Failed or rejected generations

- player_idle initial exec-91bce099-c4d1-4c4b-b8e5-6c21d03b36c2.png: rejected, black/brown glowing background.
- zombie_idle initial exec-78127531-b7af-41a3-9793-ea27d871fe36.png: rejected, background brightness variation.
- raider_idle initial exec-72540156-415d-45d7-95de-ddab662f30c7.png: rejected, green zombie skin.

> zombie_idle / raider_idle 은 걷기 프레임과 완전히 다른 캐릭터로 나와서 제외했다.
> 대기 자세는 각 걷기 세트의 첫 프레임을 쓴다 (game.js ASSET_OF).

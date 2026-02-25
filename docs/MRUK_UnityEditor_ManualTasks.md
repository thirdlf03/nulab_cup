# MRUK + ThrowableCube 手動設定チェックリスト

このプロジェクトで、`MRUK` の空間シーンと `ThrowableCube` の物理表現を有効化するために、Unity Editor で先生が手動確認する項目です。

## 1. シーン側セットアップ

- [ ] `MainXR` シーンを開く
- [ ] Hierarchy に `MRUK` コンポーネント付き GameObject が存在することを確認
- [ ] `MRUK.Scene Settings` の `Data Source` がデバイス空間シーンを読む設定（`Device` または `DeviceWith...Fallback`）になっていることを確認
- [ ] `Load Scene On Startup` を ON にする（起動時に空間シーンを自動ロード）
- [ ] 実機で空間シーン権限ダイアログが出ることを確認し、許可する

## 2. ThrowableCube プレハブ設定

- [ ] `Assets/Prefabs/Throwables/ThrowableCube.prefab` を開く
- [ ] `ThrowableCube` コンポーネントで以下を確認
- [ ] `Enable Mruk Ground Physics` = ON
- [ ] `Ground Ray Distance` が実環境サイズに対して十分な値（目安: `2.0` 以上）
- [ ] `Ground Snap Distance` は `0.02` - `0.05` から調整
- [ ] `Bounce` と `Tangent Damping` を好みの反発/減衰に調整

## 3. CubeSpawner プレハブ設定

- [ ] `Assets/Prefabs/Throwables/ThrowableCubeSpawner.prefab` を開く
- [ ] `CubeSpawner` コンポーネントで以下を確認
- [ ] `Use Mruk Floor Height` = ON
- [ ] `Floor Ray Start Height` は頭上から床を取れる高さに設定（目安: `2.0` - `3.0`）
- [ ] `Floor Ray Distance` は部屋の高さより長めに設定（目安: `5.0` - `7.0`）
- [ ] 必要なら `Debug Floor Ray` を ON にして Scene ビューでレイ確認

## 4. 動作確認タスク

- [ ] 実機でアプリ起動後、空間シーンがロードされることを確認
- [ ] サムズアップで生成したキューブが床面に対して自然に落下/接地することを確認
- [ ] キューブを投げた際、床接触時に不自然なめり込みが減っていることを確認
- [ ] 必要に応じて `Bounce` / `Ground Snap Distance` / `Rain Height` を再調整

## 5. 既知の補足

- この実装は「床判定」を中心にしています。
- 壁や家具に対する衝突をより強く反映したい場合は、MRUK の `EffectMesh` などで環境コライダーを追加するのが次段階です。

## 6. 環境コライダー追加手順（EffectMesh）

- [ ] `MainXR` シーンを開く
- [ ] `Meta` の Building Blocks から `Effect Mesh` をシーンに追加（`Default` か `Global Mesh` を選択）
- [ ] 追加された `EffectMesh` オブジェクトの `EffectMesh` コンポーネントで `Colliders` を ON
- [ ] 見た目を出したくない場合は `Hide Mesh` を ON（コライダーだけ残す）
- [ ] `Spawn On Start` を `CurrentRoomOnly`（通常）か `AllRooms`（複数部屋を使う場合）に設定
- [ ] `Labels` を用途に合わせて設定
- [ ] 最低限: `FLOOR`
- [ ] 推奨: `FLOOR`, `WALL_FACE`, `TABLE`, `COUCH`, `GLOBAL_MESH`
- [ ] 実機再生して、投げたキューブが床/壁/家具で衝突することを確認

### 注意

- `EffectMesh` のデフォルトは `Colliders = OFF` です。ON にしないと物理衝突は出ません。
- `GLOBAL_MESH` はカバー範囲が広い代わりに、面の精度が粗い場合があります。必要に応じて `Labels` を調整してください。

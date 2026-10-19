# Attribute bank format

SMAT version 1 stores clip attribute metadata separately from compressed skeletal
poses. Load it with the internal `AnimationClipAttributeBank.Read`, then pass it
to `RawAnimationClipSet.ReadWithAttributes`. The existing pose format stays
unchanged. Every loaded pose clip must have a matching bank entry; extra entries
may support another pose subset. Clip names match case-insensitively.

All numbers are little endian. Counts are unsigned 32-bit integers. Text starts
with an unsigned byte length followed by that many UTF-8 bytes.

| Record | Fields in order |
| --- | --- |
| Header | Four ASCII bytes `SMAT`, version `1`, clip count |
| Clip | Text name, six unsigned 32-bit identity words, attribute count |
| Attribute | Five unsigned 32-bit key words, float32 start and end, signed byte kind, six unsigned 32-bit value words, curve-key count |
| Curve key | Float32 time, float32 value |

Attribute records retain their order and duplicate keys. The first record whose
key and timing status pass the query supplies the result. Value words retain
their bit patterns; they can contain encoded identities as well as floats.
Kinds 0, 1 and 3 copy one, four and six words respectively. Kind 2 samples its
curve. Other signed kinds preserve the caller's payload while updating the query
metadata. Unused output lanes are never cleared implicitly.

Kind 2 requires at least one finite curve key, with nondecreasing times. Other
kinds require zero curve keys. Interval values must be finite. Counts must fit
within the available bytes before allocation; duplicates at the clip level,
truncation, invalid curves and trailing bytes are rejected.

An empty clip entry explicitly declares no attributes. A pose file loaded without
a bank has unknown attributes, and `TryGetAuthoredAttribute` throws until metadata
is provided. These states must remain distinct. The explicit-record query method
is still available to callers that own their metadata separately.

The bank reader and clip binding are implemented. Live asset selection, composite
attribute queries and authored-tree construction still need integration.

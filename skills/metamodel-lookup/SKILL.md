---
name: metamodel-lookup
description: Look up the structure of a UML 2.5.1 metaclass - its owned and inherited features, generalizations/specializations, multiplicities, redefinitions/subsettings, and OCL constraints. Use when the user asks "what is a Classifier", "what attributes does Class own", "what does Property inherit from", "is Interface abstract", or names any UML metaclass, enumeration, or primitive type.
---

# Metamodel lookup

Answers questions about the **structure of the UML 2.5.1 metamodel itself** - metaclasses (Class,
Classifier, Property, Association, ...), enumerations (VisibilityKind, AggregationKind, ...),
primitive types (Integer, String, ...), and the metamodel's own association *links* between
metaclasses (e.g. the association behind `Class::ownedAttribute`, named things like
`A_ownedAttribute_class` - distinct from the `Association` metaclass itself, which is a `kind:
"class"` entry like any other metaclass). Not for Standard Profile stereotypes (`«Trace»`,
`«Create»`, ...) - see the `standard-profile-lookup` skill for those. Not for verbatim UML spec
prose - see `uml-spec-citation`.

## Read order

1. `knowledge/installed.json` - resolve the default UML version (the `default` field). If the file
   doesn't exist or has no versions, tell the user to run `uml4net-sage fetch` first (see the
   `knowledge-setup` skill) and stop.
2. `knowledge/<version>/metamodel/index.json` - an array of `{name, kind, package, qualifiedName,
   isAbstract, file}` rows. Find the row for the element the user asked about (case-insensitive
   match on `name`; if ambiguous, list the matches and ask). `kind` is `"class"`, `"enumeration"`,
   `"primitiveType"`, or `"association"`.
3. `knowledge/<version>/metamodel/elements/<file>` (the `file` field from the index row) - the full
   per-element markdown page. For `kind: "class"`/`"enumeration"`/`"primitiveType"`: front matter,
   `## Generalizations`, `## Specializations`, `## Owned features` (each feature's type,
   multiplicity, modifiers - `derived`, `union` (derived union), `readOnly`, `static`, `ordered`,
   `non-unique`, `composite`, `shared`, `query`, `abstract` - plus `default`, `opposite` (the other end
   of the feature's binary association), `redefines` and `subsets` sub-bullets), `## Inherited
   features` (UML's `inheritedMember`, precomputed: every ancestor's attributes and operations minus
   those a more specific metaclass redefines - never re-derive this by hand-walking generalizations,
   and don't expect a redefined member to be listed), `## Constraints` (OCL,
   tagged MODEL tier since it's read directly from the metamodel XMI), `## Description` (the
   element's own defining comment from the XMI, also MODEL tier - not a verbatim spec quote, even
   where the wording is close; for that, see `uml-spec-citation`). For `kind: "association"` the
   page's shape is different - no Generalizations/Owned features sections, just `## Member ends`
   (each end's type/multiplicity/modifiers, including `non-navigable`, flagged `owned by this
   association` when the end is one of the association's own `ownedEnd` properties rather than an
   attribute you'd find on either endpoint classifier's own page) and `## Description`.

## Set/closure questions

For questions like "every concrete subclass of Classifier" or "which metaclasses have a feature
typed by ValueSpecification", prefer `jq` over `knowledge/<version>/metamodel/metamodel.json` (a
single JSON document with every class's `allAncestors`, `allDescendants`, `directSubclasses`,
`ownedAttributes`, `ownedOperations`, `inheritedAttributes`, `inheritedOperations`, and `constraints`
precomputed - each feature carrying `isDerived`/`isDerivedUnion`/`isReadOnly`/`isStatic`/
`aggregation`/`opposite`/`defaultValue` (attributes) or `isQuery`/`isAbstract` (operations) - plus a
top-level `associations` array with each association's `memberEnds` (each with `isNavigable`,
`aggregation`, `isDerived` and `opposite`) - no JSON library required, and
cheaper than reading every element file). If `jq` isn't installed, fall back to `Read`/`Grep` over
the per-element markdown files; it works but costs more context.

Examples:
- `jq '.classes[] | select(.isAbstract == false and (.allAncestors | index("UML::Classification::Classifier")) != null) | .name' knowledge/2.5.1/metamodel/metamodel.json`
- `jq '.associations[] | select(any(.memberEnds[]; .isNavigable | not)) | .name' knowledge/2.5.1/metamodel/metamodel.json` - every association with a non-navigable end
- `jq '.classes[] | select(.name == "Class") | .ownedAttributes[] | select(.name == "nestedClassifier") | .opposite' knowledge/2.5.1/metamodel/metamodel.json` - the opposite end of `Class::nestedClassifier`
- `jq '[.classes[] | .ownedAttributes[] | select(.isDerivedUnion) | "\(.ownerQualifiedName)::\(.name)"]' knowledge/2.5.1/metamodel/metamodel.json` - every derived union

## Answering

- Always state which UML version the answer is for (e.g. "In UML 2.5.1, ...").
- Tag facts read from `metamodel.json`/the element markdown as **MODEL** tier (read directly from
  the metamodel XMI) - distinct from **NORMATIVE** (verbatim spec quote, see `uml-spec-citation`) and
  **DERIVED** (computed/inferred here, e.g. "no clause reference is available").
- Link to related metaclasses by name so the user can ask a follow-up ("tell me more about
  Property") rather than dumping the whole inheritance chain unprompted.

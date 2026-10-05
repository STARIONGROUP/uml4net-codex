---
name: standard-profile-lookup
description: Look up a UML 2.5.1 Standard Profile stereotype (e.g. Create, Destroy, Trace, Refine, Derive, Focus, Metaclass, ModelLibrary, Framework, Utility) - what metaclass it extends and what it means. Use when the user asks "what does the Trace stereotype mean", "what can Create be applied to", or names a stereotype in guillemets like «Trace».
---

# Standard Profile lookup

Answers questions about the OMG UML 2.5.1 **Standard Profile** - the fixed set of stereotypes
(`«Trace»`, `«Create»`, `«Destroy»`, `«Refine»`, `«Derive»`, `«Focus»`, `«Metaclass»`,
`«ModelLibrary»`, `«Framework»`, `«Utility»`, ...) that OMG ships as part of the UML specification
itself. Not for metamodel structure (a stereotype is not a metaclass) - see `metamodel-lookup` for
that, including for the primitive types (Integer, String, ...), which live in the metamodel index,
not here.

## Read order

1. `knowledge/installed.json` - resolve the default UML version.
2. `knowledge/<version>/standard-profile/index.json` - an array of `{qualifiedName, kind, file,
   source, profile, baseMetaclasses}` rows, one per stereotype (`kind` is always `"stereotype"`,
   `source` is `"StandardProfile"`, `profile` is the owning profile's qualified name,
   `baseMetaclasses` the metaclasses the stereotype itself extends). For "which stereotypes can be
   applied to X" questions, filter this index on `baseMetaclasses` rather than opening every page.
3. `knowledge/<version>/standard-profile/pages/<file>` - front matter with `profile` and
   `baseMetaclasses` (which metaclasses the stereotype can be applied to) plus `## Base metaclasses`
   (each marked *(required)* when the extension is required - every instance of that metaclass must
   then carry the stereotype), `## Tagged values`
   (the stereotype's own attributes, i.e. what you'd set when applying it), and `## Description`
   (the OMG documentation comment, when present in the source XMI - some stereotypes have none).

## Answering

- State the UML version.
- Name every base metaclass a stereotype extends, and say when an extension is required - a
  stereotype can extend more than one (in UML 2.5.1, `«Create»` extends both `BehavioralFeature`
  and `Usage`), so always read the actual `baseMetaclasses` list rather than assuming a single one
  or answering from memory. A stereotype also applies wherever its generalizations do:
  the listed base metaclasses are only the ones it extends itself, so check `## Generalizations` too.
- Tag facts as **MODEL** tier (read directly from `StandardProfile.xmi`). If `## Description` says
  "_No description available._", say so plainly rather than inventing an explanation - the OMG
  Standard Profile XMI itself omits prose for some stereotypes.

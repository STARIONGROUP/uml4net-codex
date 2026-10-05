---
name: uml-xmi-model-inspection
description: Check a user-supplied .xmi or .uml UML model FILE for conformance to the UML 2.5.1 metamodel - unresolved references, abstract or unknown metaclasses, duplicate ids, multiplicity violations, misapplied stereotypes, and other reader-level diagnostics. Use when the user shares a UML model file (serialized as XMI) and asks to validate it, check it for errors, or find problems in it. Does not quote the XMI specification's own normative text - see xmi-spec-citation for that.
---

# UML XMI model inspection

Loads a user-supplied `.xmi`/`.uml` **UML model file** (content serialized in XMI form) with
`uml4net.xmi` - the same reader the knowledge base itself is built with - and checks it against the
generated metamodel graph. This skill is about checking one specific file's UML content; for
questions about what the XMI standard itself requires or means, independent of any particular file
(e.g. the meaning of `href`/`idref`, or cross-document linking rules), use `xmi-spec-citation`
instead - that skill is about the XMI serialization standard, not UML content.

## Running the check

Run the CLI directly (this is the one skill that executes the CLI rather than only reading the
knowledge base):

```bash
uml4net-sage inspect <path-to-file> --json
```

If the model references other documents through `pathmap://` URIs (Papyrus/Eclipse UML2 and
MagicDraw exports often do, e.g. for their standard libraries), map them to local files or
directories with `--pathmap <uri>=<path>` (repeatable); a prefix mapped to a directory, such as
`--pathmap pathmap://UML_LIBRARIES=./libraries`, resolves every document under it. An unmapped
`pathmap://` reference is reported as an `unresolved-reference` whose message says no `--pathmap`
was given for it.

If the CLI reports the version hasn't been generated yet, run the `knowledge-setup` skill's fetch +
generate steps first.

The `--json` output is an `InspectionReport`: `{ModelPath, UmlVersion, Findings: [{Severity,
Category, ElementXmiId, Message, Line}]}`, ordered by `Line`. `Severity` is `"error"` or
`"warning"`; `ElementXmiId` and `Line` (1-based, in the inspected file) are given whenever the
finding is about a specific element. Categories:

- **`abstract-instantiation`** - an element whose `xmi:type` is a metaclass that is abstract in the
  UML metamodel (e.g. `Classifier`, `Type`); only its concrete specializations (`Class`, `DataType`,
  ...) can be instantiated.
- **`unknown-metaclass`** - an `xmi:type` in the UML namespace that is not a UML metaclass at all
  (often a typo, or a tool-specific type).
- **`duplicate-xmi-id`** - an `xmi:id` declared more than once; reported at its second declaration,
  with every line it occurs on. Readers keep only the first.
- **`read-aborted`** - `uml4net.xmi` could not read the file (most often because of one of the two
  `xmi:type` problems above); only the `xmi:type`/`xmi:id` checks ran.
- **`unresolved-reference`** - a reference (`idref`, `href`, or an attribute holding ids) whose
  target could not be found, naming the referencing element, the property and the identifier.
- **`invalid-reference`** - a reference that resolved but cannot be used: its target has a type the
  property doesn't allow, is already owned elsewhere, or exceeds the property's multiplicity.
- **`multiplicity-violation`** - an element with fewer (or more) values for a non-derived metamodel
  feature than that feature's multiplicity allows, e.g. a `Generalization` without its required
  `general`. Features with a default value are not checked, since XMI omits values equal to their
  default.
- **`stereotype-misapplied`** - a stereotype applied to an element whose metaclass neither the
  stereotype nor any of its generals extends.
- **`stereotype-target-unresolved`** - a stereotype application whose `base_<Metaclass>` element
  doesn't exist.
- **`stereotype-unresolved`** (warning) - a stereotype application whose profile couldn't be loaded,
  so it wasn't checked; place the profile next to the model or map it with `--pathmap`.
- **`reader-diagnostic`** - any other warning or error the XMI reader emitted (e.g. an invalid
  enumeration literal, an element or attribute it didn't recognise).

## Known scope

Not checked: OCL constraints of the metamodel (well-formedness rules such as "a classifier may not
generalize itself"), whether a `redefinedProperty`/`subsettedProperty` is actually consistent with
what it redefines or subsets, and anything about a referenced document other than whether the
reference resolves. Mention this if the user asks for a check outside that list, rather than
implying full validation coverage.

## Answering

- Report findings grouped by severity, most severe first.
- Cite each finding's `Line` (and `ElementXmiId`) so the user can jump to it.
- For each `abstract-instantiation` finding, explain *why* it matters using `metamodel-lookup` if
  useful (e.g. "Classifier is abstract because ... - see its Specializations for the concrete
  metaclasses you can use instead"). Likewise for `multiplicity-violation` (what the feature means)
  and `stereotype-misapplied` (what the stereotype extends - see `standard-profile-lookup` for the
  Standard Profile's own stereotypes).
- For each `unresolved-reference`, `duplicate-xmi-id` or `reader-diagnostic` finding, where the
  underlying mechanism is governed by the XMI standard itself rather than the UML metamodel (e.g. an
  unresolved `href` - governed by XMI's
  cross-document linking rules, clause 7.10), name the relevant XMI clause and use
  `xmi-spec-citation` to quote or cite it, the same way `abstract-instantiation` findings lean on
  `metamodel-lookup`. Not every finding maps to a specific clause - only do this when it
  genuinely clarifies the finding, not as a rote addition to every message.
- If there are zero findings, say so plainly - don't imply a clean bill of health beyond what the
  implemented checks actually cover (see "Known scope" above).

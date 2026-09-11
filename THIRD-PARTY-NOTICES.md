# Third-party notices

Direct package licenses below follow the published NuGet metadata.
Package references are maintained in the project files.

| Dependency | Version | License |
|---|---|---|
| [Serilog](https://www.nuget.org/packages/Serilog/4.0.0) | 4.0.0 | Apache-2.0 |
| [Serilog.Extensions.Logging](https://www.nuget.org/packages/Serilog.Extensions.Logging/8.0.0) | 8.0.0 | Apache-2.0 |
| [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/6.0.0) | 6.0.0 | Apache-2.0 |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/8.0.2) | 8.0.2 | MIT |
| [Microsoft.Extensions.*](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) | Transitive versions resolved by NuGet | MIT; verify the restored dependency closure before release. |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.12.0) | 17.12.0 | MIT |
| [Microsoft.NETFramework.ReferenceAssemblies](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies/1.0.3) | 1.0.3 | Microsoft license; verify the resolved framework package terms. |
| [xunit](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | Apache-2.0 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/3.0.2) | 3.0.2 | Apache-2.0 |
| [ILRepack](https://www.nuget.org/packages/ILRepack/2.0.46) | 2.0.46 | Apache-2.0 |
| [JetBrains.Annotations](https://www.nuget.org/packages/JetBrains.Annotations/2026.2.0) | 2026.2.0 | MIT |
| [Nice3point.Revit.Sdk](https://www.nuget.org/packages/Nice3point.Revit.Sdk/6.2.3) | 6.2.3 | MIT |
| [Nice3point.Revit.Api.RevitAPI](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI) | `$(RevitVersion).*` | Package license: MIT. Autodesk assembly redistribution terms: verify. |
| [Nice3point.Revit.Api.RevitAPIUI](https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPIUI) | `$(RevitVersion).*` | Package license: MIT. Autodesk assembly redistribution terms: verify. |
| [Lucide icon geometry](https://github.com/lucide-icons/lucide/blob/main/LICENSE) | Source revision not recorded | ISC; Feather-derived icons use MIT. Verify the retained icon provenance before release. |

The retained settings and journal icon paths are derived from Lucide.
Their source revision was not recorded with the geometry.
The MIT project license does not replace dependency licenses or Autodesk's terms.

## Release verification

Verify the full restored dependency closure and any required notices in distributed binaries.
The Windows restore has not been run for this checkout.
ILRepack merges runtime dependencies into the add-in assembly.
Include applicable dependency notices with release packages.

## Lucide license notice

```text
ISC License

Copyright (c) 2026 Lucide Icons and Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.

---

The following Lucide icons are derived from the Feather project:

airplay, alert-circle, alert-octagon, alert-triangle, aperture, arrow-down-circle, arrow-down-left, arrow-down-right, arrow-down, arrow-left-circle, arrow-left, arrow-right-circle, arrow-right, arrow-up-circle, arrow-up-left, arrow-up-right, arrow-up, at-sign, calendar, cast, check, chevron-down, chevron-left, chevron-right, chevron-up, chevrons-down, chevrons-left, chevrons-right, chevrons-up, circle, clipboard, clock, code, columns, command, compass, corner-down-left, corner-down-right, corner-left-down, corner-left-up, corner-right-down, corner-right-up, corner-up-left, corner-up-right, crosshair, database, divide-circle, divide-square, dollar-sign, download, external-link, feather, frown, hash, headphones, help-circle, info, italic, key, layout, life-buoy, link-2, link, loader, lock, log-in, log-out, maximize, meh, minimize, minimize-2, minus-circle, minus-square, minus, monitor, moon, more-horizontal, more-vertical, move, music, navigation-2, navigation, octagon, pause-circle, percent, plus-circle, plus-square, plus, power, radio, rss, search, server, share, shopping-bag, sidebar, smartphone, smile, square, table-2, tablet, target, terminal, trash-2, trash, triangle, tv, type, upload, x-circle, x-octagon, x-square, x, zoom-in, zoom-out

The MIT License (MIT) (for the icons listed above)

Copyright (c) 2013-present Cole Bemis

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

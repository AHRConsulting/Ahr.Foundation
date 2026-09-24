#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_dir="${2:-$repo_root/artifacts/packages}"
version="${1:-}"

if [[ -z "$version" ]]; then
  packages=("$package_dir"/Ahr.Foundation.*.nupkg)
  [[ ${#packages[@]} -eq 1 && -f "${packages[0]}" ]]
  package_name="$(basename "${packages[0]}")"
  version="${package_name#Ahr.Foundation.}"
  version="${version%.nupkg}"
fi

package="$package_dir/Ahr.Foundation.$version.nupkg"
sample="$repo_root/samples/Ahr.Foundation.Samples/Ahr.Foundation.Samples.csproj"
verification_dir="$repo_root/artifacts/package-verification/aot-smoke"
nuget_config="$verification_dir/NuGet.config"
global_packages="$verification_dir/packages"
rid="$(dotnet --info | awk -F': *' '/^ *RID:/ {print $2; exit}')"

test -f "$package"
[[ -n "$rid" ]]

rm -rf "$verification_dir"
mkdir -p "$verification_dir"

cat > "$nuget_config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="$global_packages" />
  </config>
  <packageSources>
    <clear />
    <add key="local" value="$package_dir" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="Ahr.Foundation" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

dotnet publish "$sample" \
  --configfile "$nuget_config" \
  -r "$rid" \
  -c Release \
  --property:UseLocalAhrFoundationPackage=true \
  --property:AhrFoundationPackageVersion="$version" \
  --property:PublishAot=true \
  --property:InvariantGlobalization=true \
  --property:RestoreNoCache=true

publish_dir="$repo_root/samples/Ahr.Foundation.Samples/bin/Release/net10.0/$rid/publish"
binary="$publish_dir/Ahr.Foundation.Samples"

test -x "$binary"

output="$("$binary")"

grep -Fq "All sample scenarios executed successfully." <<<"$output"

echo "Verified Ahr.Foundation $version publishes and runs under NativeAOT ($rid) via the packaged sample consumer."

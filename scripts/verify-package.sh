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
symbols="$package_dir/Ahr.Foundation.$version.snupkg"
sample="$repo_root/samples/Ahr.Foundation.Samples/Ahr.Foundation.Samples.csproj"
verification_dir="$repo_root/artifacts/package-verification"
nuget_config="$verification_dir/NuGet.config"
global_packages="$verification_dir/packages"
analyzer_smoke="$verification_dir/analyzer-smoke"

test -f "$package"
test -f "$symbols"

package_contents="$(unzip -Z1 "$package")"
symbols_contents="$(unzip -Z1 "$symbols")"

for path in \
  Ahr.Foundation.nuspec \
  README.md \
  CHANGELOG.md \
  package-icon.png \
  lib/netstandard2.0/Ahr.Foundation.dll \
  lib/netstandard2.0/Ahr.Foundation.xml \
  lib/net10.0/Ahr.Foundation.dll \
  lib/net10.0/Ahr.Foundation.xml \
  analyzers/dotnet/cs/Ahr.Foundation.Analyzers.dll; do
  grep -Fxq "$path" <<<"$package_contents"
done

for path in \
  lib/netstandard2.0/Ahr.Foundation.pdb \
  lib/net10.0/Ahr.Foundation.pdb; do
  grep -Fxq "$path" <<<"$symbols_contents"
done

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
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="Ahr.Foundation" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

dotnet restore "$sample" \
  --configfile "$nuget_config" \
  --force \
  --no-cache \
  --property:UseLocalAhrFoundationPackage=true \
  --property:AhrFoundationPackageVersion="$version"

dotnet run \
  --project "$sample" \
  --configuration Release \
  --no-restore \
  --property:UseLocalAhrFoundationPackage=true \
  --property:AhrFoundationPackageVersion="$version"

mkdir -p "$analyzer_smoke"
cat > "$analyzer_smoke/AnalyzerSmoke.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Ahr.Foundation" VersionOverride="$version" />
  </ItemGroup>
</Project>
EOF

cat > "$analyzer_smoke/Program.cs" <<'EOF'
using Ahr.Foundation;

Result invalid = default;
EOF

if dotnet build "$analyzer_smoke/AnalyzerSmoke.csproj" \
  --configfile "$nuget_config" \
  --no-incremental \
  --property:RestoreNoCache=true \
  > "$analyzer_smoke/build.log" 2>&1; then
  echo "The packaged AHRF001 analyzer did not reject default Result construction." >&2
  exit 1
fi

grep -Fq "AHRF001" "$analyzer_smoke/build.log"

echo "Verified Ahr.Foundation $version through the packaged sample consumer."
echo "Verified packaged analyzer delivery with AHRF001."

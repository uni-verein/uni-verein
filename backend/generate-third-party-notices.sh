#!/bin/bash
set -e

# ============================================================
# generate-third-party-notices.sh
# Automatically generates a THIRD-PARTY-NOTICES.txt for all
# NuGet dependencies (including transitive) of the solution.
# ============================================================

# --- Configuration ---
SOLUTION_FILE="UniVerein.sln"
EXPORT_DIR="./license-export"
TEMP_DIR="./temp-license"  # Tool wird temp-license/temp-license erstellen
JSON_FILE="$EXPORT_DIR/licenses.json"
OUTPUT_FILE="./THIRD-PARTY-NOTICES.md"

# --- Check prerequisites ---
echo "🔍 Checking prerequisites..."

if ! command -v dotnet-project-licenses &> /dev/null; then
    echo "❌ 'dotnet-project-licenses' is not installed."
    echo "   Installation with: dotnet tool install --global dotnet-project-licenses"
    exit 1
fi

if ! command -v jq &> /dev/null; then
    echo "❌ 'jq' is not installed."
    echo "   Installation (Windows/Choco): choco install jq"
    echo "   Installation (Ubuntu/WSL):    sudo apt install jq"
    exit 1
fi

if [ ! -f "$SOLUTION_FILE" ]; then
    echo "❌ Solution file '$SOLUTION_FILE' was not found."
    echo "   Please run script in the backend directory or adjust the path."
    exit 1
fi

echo "✅ All prerequisites fulfilled."
echo ""

# --- Delete old export and create new directory ---
if [ -d "$EXPORT_DIR" ]; then
    echo "🧹 Removing old export directory..."
    rm -rf "$EXPORT_DIR"
fi
if [ -d "$TEMP_DIR" ]; then
    echo "🧹 Removing old temp directory..."
    rm -rf "$TEMP_DIR"
fi

echo "📁 Creating export and temp directory..."
mkdir -p "$EXPORT_DIR"
mkdir -p "$TEMP_DIR"

# --- Export license data + texts ---
echo "📦 Collecting NuGet license information (including transitive packages)..."
dotnet-project-licenses -i "$SOLUTION_FILE" \
  --include-transitive \
  --unique \
  --json \
  --outfile "licenses.json" \
  --export-license-texts \
  --output-directory "$TEMP_DIR" \
  --log-level Error
  
# --- Move to final location ---
mkdir -p "$EXPORT_DIR"
mv "$TEMP_DIR/temp-license"/* "$EXPORT_DIR/" 2>/dev/null || mv "$TEMP_DIR"/* "$EXPORT_DIR/" 2>/dev/null
rm -rf "$TEMP_DIR"

if [ ! -f "$JSON_FILE" ]; then
    echo "❌ Error: '$JSON_FILE' was not created."
    exit 1
fi

echo "✅ License data successfully collected."
echo ""

# --- Check JSON structure (output first element for diagnostics) ---
echo "🔎 Structure of first license entry (for verification):"
jq '.[0]' "$JSON_FILE"
echo ""

# --- Generate THIRD-PARTY-NOTICES.txt ---
echo "📝 Generating THIRD-PARTY-NOTICES.txt..."

{
    echo "THIRD-PARTY NOTICES"
    echo "===================="
    echo ""
    echo "This project (UniVerein Backend) uses the following open-source packages."
    echo "Generated on: $(date '+%Y-%m-%d %H:%M:%S')"
    echo ""
} > "$OUTPUT_FILE"

# --- Process JSON entries ---
jq -r '.[] | @base64' "$JSON_FILE" | while read -r entry; do
    # Decode base64 entry
    json=$(echo "$entry" | base64 -d)
    
    # Extract fields using correct JSON fieldnames
    package_name=$(echo "$json" | jq -r '.PackageName // "Unknown"')
    package_version=$(echo "$json" | jq -r '.PackageVersion // "Unknown"')
    license_type=$(echo "$json" | jq -r '.LicenseType // "Unknown"')
    license_url=$(echo "$json" | jq -r '.LicenseUrl // ""')
    copyright=$(echo "$json" | jq -r '.Copyright // ""')
    
    {
        echo "--------------------------------------------------------------------------------"
        echo "Package: $package_name"
        echo "Version: $package_version"
        echo "License: $license_type"
        if [ -n "$license_url" ] && [ "$license_url" != "null" ] && [ "$license_url" != "" ]; then
            echo "License URL: $license_url"
        fi
        if [ -n "$copyright" ] && [ "$copyright" != "null" ] && [ "$copyright" != "" ]; then
            echo "Copyright: $copyright"
        fi
        echo ""
    } >> "$OUTPUT_FILE"

done

{
    echo ""
    echo "== End of Third-Party Notices =="
} >> "$OUTPUT_FILE"

echo "✅ THIRD-PARTY-NOTICES.txt successfully generated!"
echo ""
echo "📄 Preview (first 50 lines):"
head -50 "$OUTPUT_FILE"
echo ""
echo "📊 Statistics:"
total_packages=$(jq 'length' "$JSON_FILE")
echo "   Total packages: $total_packages"
echo ""

# --- Cleanup ---
rm -rf "$EXPORT_DIR" 2>/dev/null || true
rm -rf "$TEMP_DIR" 2>/dev/null || true
echo "🧹 Temporary directories cleaned up."
echo ""
echo "📁 File location: $OUTPUT_FILE"

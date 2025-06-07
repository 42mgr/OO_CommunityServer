# Find which .csproj contains your modified .cs files
for cs_file in $(cat modified-files.txt | grep "\.cs$"); do
    # Find the .csproj that includes this file
    csproj=$(find . -name "*.csproj" -exec grep -l "$cs_file" {} \;)
    if [ ! -z "$csproj" ]; then
        # Extract the assembly name from .csproj
        dll_name=$(grep -E "<AssemblyName>|<TargetName>" "$csproj" | sed -E 's/.*>(.*)<.*/\1/')
        if [ -z "$dll_name" ]; then
            # If no explicit AssemblyName, use project name
            dll_name=$(basename "$csproj" .csproj)
        fi
        echo "$cs_file -> $dll_name.dll"
    fi
done | sort -u

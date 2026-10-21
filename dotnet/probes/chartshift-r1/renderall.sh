#!/bin/bash
# usage: renderall.sh <label> <soffice>
L=$1; S=$2; O=/tmp/claude-0/chartshift/all/$L
cd /home/user/xo-retarget/dotnet/tests/corpus
for ext in docx xlsx fodt doc fods odt pptx rtf xls fodp ppt ods odp xltx ott; do
  mkdir -p $O/$ext
  find . -type f -name "*.$ext" | sort > $O/$ext.list
  FONTCONFIG_FILE=/tmp/claude-0/fc-stock/fonts.conf timeout 3000 $S --headless -env:UserInstallation=file:///tmp/claude-0/lo-profile/chartshift-all-$L --convert-to pdf --outdir $O/$ext $(cat $O/$ext.list) > $O/$ext.log 2>&1
done
echo done > $O/DONE

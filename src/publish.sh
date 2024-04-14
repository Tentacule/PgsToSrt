VERSION="1.4.5"

sed -i "s/0.0.0.0/$VERSION.0/" ./PgsToSrt/PgsToSrt.csproj
sed -i "s/0.0.0/$VERSION/" ./PgsToSrt/PgsToSrt.csproj

dotnet publish -f net6.0 --no-self-contained -o out/publish

cd out/publish
zip -r ../PgsToStr-$VERSION.zip ./*
cd ..

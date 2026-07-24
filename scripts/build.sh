#!/bin/bash

echo "========================================="
echo " KORSANCIM - C++ Backend Derleniyor...   "
echo "========================================="

# Proje kök dizininde olduğumuzdan emin olalım
CDIR=$(pwd)

# Backend build klasörüne geçelim
mkdir -p backend/build
cd backend/build

# CMake ve Make komutlarını çalıştıralım
cmake ..
make

if [ $? -eq 0 ]; then
    echo "-----------------------------------------"
    echo " ✅ Derleme Başarılı!"
    echo "-----------------------------------------"
else
    echo "-----------------------------------------"
    echo " ❌ Derleme Hatası!"
    echo "-----------------------------------------"
fi

cd $CDIR
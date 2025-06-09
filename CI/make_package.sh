#!/bin/bash

#set -e
set -x

#echo "git push"
REMOTE=$(git config --get remote.origin.url)
echo $REMOTE
COMMIT=$(git log -1 --pretty=%B)
echo $COMMIT

# read target branch provided from pipeline script
TARGET_BRANCH=$1
echo "Writing to branch '$TARGET_BRANCH'"


#####
# Generate archive for the package
git archive -o archive.tar HEAD:$FOLDER_TO_EXPORT
ARCHIVE_PATH=$(pwd)

mkdir ../$TARGET_BRANCH
cd ../$TARGET_BRANCH

if [ "$(git ls-remote https://$GIT_TOKEN@${REMOTE#*//} $TARGET_BRANCH | wc -l)" != 1 ]; then
    git clone --depth=1 https://$GIT_TOKEN@${REMOTE#*//}
    cd *
    git checkout -b $TARGET_BRANCH
else
    git clone --branch=$TARGET_BRANCH https://$GIT_TOKEN@${REMOTE#*//}
    cd *
fi

#####
# Remove all files from the repository, before unpacking the archive
shopt -s extglob
rm ./ -dr -- !(.git)
[ -f .vsconfig ] && rm .vsconfig


#####
# Unpack archive generated from FOLDER_TO_EXPORT
mv $ARCHIVE_PATH/archive.tar archive.tar

echo "Archive content:"

tar -tf archive.tar
tar -xf archive.tar --overwrite
rm archive.tar


#####
# Update package contents after unpacking

# Update 'Samples' directory since it should not be seen by the main package
[ -f Samples.meta ] && rm Samples.meta
[ -d Samples ] && mv Samples Samples~


#####
# "Packaging Pete" generates the commit for the UPM package

git add -A

echo "Diffs:"
git diff --cached

git config --global user.email "pete@petesprofessionalpackagingparlor.io"
git config --global user.name "Packaging Pete"

git commit -m "$COMMIT"

git push https://$GIT_TOKEN@${REMOTE#*//} $TARGET_BRANCH
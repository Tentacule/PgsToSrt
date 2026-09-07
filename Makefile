# Variables
REPOSITORY := tentacule/pgstosrt
TESSDATA_DIR  := tessdata
LANGUAGE   := eng
TAG_ALL    := latest

# Makefile.env
ifneq (,$(wildcard ./Makefile.env))
	include Makefile.env
	export
endif

##
##@ General
##

## Print this message and exit
.PHONY: help
help:
	@awk '																								\
		BEGIN { 																						\
			printf "\nUsage:\n  make \033[36m<target>\033[0m\n"											\
		}																								\
		END {																							\
			printf "\n"																					\
		}																								\
		/^[0-9A-Za-z-]+:/ {																				\
			if (prev ~ /^## /) {																		\
				printf "  \x1b[36m%-23s\x1b[0m %s\n", substr($$1, 0, length($$1)-1), substr(prev, 3)	\
			}																							\
		}																								\
		/^##@/ {																						\
			printf "\n\033[1m%s\033[0m\n", substr($$0, 5)												\
		}																								\
		!/^\.PHONY/ {																					\
			prev = $$0																					\
		}																								\
	' $(MAKEFILE_LIST)


##
##@ Supplemental
##

## Download tesseract-ocr data files
.PHONY: tessdata
tessdata:
	mkdir -p $(TESSDATA_DIR)
	test -f $(TESSDATA_DIR)/$(LANGUAGE).traineddata || \
		curl -fL https://github.com/tesseract-ocr/tessdata/raw/main/$(LANGUAGE).traineddata \
			-o $(TESSDATA_DIR)/$(LANGUAGE).traineddata


##
##@ Image
##

## Build the docker image, baking in the tessdata found in TESSDATA_DIR (options: TESSDATA_DIR=tessdata)
build:
	docker build . \
		--file Dockerfile \
		--tag $(REPOSITORY):$(TAG_ALL) \
		--build-arg TESSDATA_DIR=$(TESSDATA_DIR)

## Push the docker image
push:
	docker push $(REPOSITORY):$(TAG_ALL)

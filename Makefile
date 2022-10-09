# Variables
LANGUAGE := eng

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
tessdata:
	git clone --depth=1 https://github.com/tesseract-ocr/tessdata.git


##
##@ Single language
##

## Build a single-language docker image (options: LANGUAGE=eng)
build-single: tessdata
	docker build . \
		--file Dockerfile \
		--tag tzvetkoff/pgs2srt:$(LANGUAGE) \
		--build-arg LANGUAGE=$(LANGUAGE)

## Push a single-language docker image (options: LANGUAGE=eng)
push-single:
	docker push tzvetkoff/pgs2srt:$(LANGUAGE)


##
##@ Multi language
##

## Build all-languages docker image (default language is `eng`)
build-all: tessdata
	docker build . \
		--file Dockerfile \
		--tag tzvetkoff/pgs2srt:all \
		--build-arg LANGUAGE=*

## Push all-languages docker image
push-all:
	docker push tzvetkoff/pgs2srt:all

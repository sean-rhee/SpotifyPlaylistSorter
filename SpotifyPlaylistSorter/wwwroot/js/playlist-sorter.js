(() => {
    const proposedList = document.querySelector("#proposed-track-list");
    const artistList = document.querySelector("[data-artist-order-list]");
    const artistFind = document.querySelector("#artist-find-input");
    const artistFindStatus = document.querySelector("#artist-find-status");
    const movedItemCount = document.querySelector("#moved-item-count");
    const proposedDescription = document.querySelector("#proposed-order-description");
    const orderInputs = document.querySelectorAll("[data-order-input]");
    const saveButtons = document.querySelectorAll(".save-order-button");
    const saveStatus = document.querySelector("#save-order-status");

    if (!proposedList || !artistList || !movedItemCount || !proposedDescription) {
        return;
    }

    const normalize = value => (value ?? "").trim().toLocaleLowerCase();
    const originalArtistOrder = Array.from(artistList.children)
        .map(item => item.dataset.artistName);
    let appliedArtistOrder = [...originalArtistOrder];
    let draggedArtist = null;
    let positionChangeTimeout = null;
    let matchingArtists = [];
    let currentArtistMatchIndex = -1;

    const originalPosition = row => Number.parseInt(row.dataset.originalPosition, 10);
    const artistRanks = () => new Map(
        appliedArtistOrder.map((name, index) => [normalize(name), index]));
    const syncSaveControls = moved => {
        const order = Array.from(proposedList.children)
            .map(row => originalPosition(row))
            .join(",");
        orderInputs.forEach(input => {
            input.value = order;
        });
        saveButtons.forEach(button => {
            button.disabled = moved === 0 || button.dataset.saveSupported !== "true";
        });
        if (saveStatus) {
            saveStatus.textContent = moved === 0
                ? "Choose a sort to enable saving."
                : `${moved} ${moved === 1 ? "item" : "items"} will move.`;
        }
    };
    const refreshArtistIndices = () => {
        const artistItems = Array.from(artistList.children);
        artistItems.forEach((item, index) => {
            const positionInput = item.querySelector(".artist-order-index");
            positionInput.value = index + 1;
            positionInput.max = artistItems.length;
        });
    };
    const renderArtistOrder = order => {
        const artistItems = new Map(
            Array.from(artistList.children)
                .map(item => [normalize(item.dataset.artistName), item]));
        order.forEach(name => artistList.append(artistItems.get(normalize(name))));
        refreshArtistIndices();
    };
    const clearArtistSearch = () => {
        Array.from(artistList.children).forEach(item => {
            item.classList.remove("artist-search-match", "artist-search-current");
        });
        matchingArtists = [];
        currentArtistMatchIndex = -1;
        if (artistFindStatus) {
            artistFindStatus.textContent = "";
        }
    };
    const showArtistMatch = matchIndex => {
        matchingArtists.forEach(item => item.classList.remove("artist-search-current"));
        currentArtistMatchIndex = (
            matchIndex + matchingArtists.length
        ) % matchingArtists.length;

        const currentArtist = matchingArtists[currentArtistMatchIndex];
        currentArtist.classList.add("artist-search-current");
        currentArtist.scrollIntoView({ block: "center", behavior: "auto" });
        if (artistFindStatus) {
            artistFindStatus.textContent = `${currentArtistMatchIndex + 1} of ${matchingArtists.length}`;
        }
    };
    const findArtists = () => {
        clearArtistSearch();
        const query = normalize(artistFind?.value);
        if (!query) {
            return;
        }

        matchingArtists = Array.from(artistList.children)
            .filter(item => normalize(item.dataset.artistName).includes(query));
        matchingArtists.forEach(item => item.classList.add("artist-search-match"));

        if (matchingArtists.length === 0) {
            if (artistFindStatus) {
                artistFindStatus.textContent = "No match";
            }
            return;
        }

        showArtistMatch(0);
    };

    const updateProposedPositions = message => {
        let moved = 0;

        Array.from(proposedList.children).forEach((row, index) => {
            const position = index + 1;
            const previousPosition = originalPosition(row);
            const positionLabel = row.querySelector(".track-position");
            const positionBlock = row.querySelector(".track-position-block");
            row.querySelector(".track-previous-position")?.remove();

            positionLabel.textContent = position;
            positionLabel.setAttribute("aria-label", `Position ${position}`);

            if (position !== previousPosition) {
                moved += 1;
                const previousLabel = document.createElement("span");
                previousLabel.className = "track-previous-position";
                previousLabel.textContent = `from ${previousPosition}`;
                positionBlock.append(previousLabel);
            }
        });

        movedItemCount.textContent = `${moved} moved`;
        movedItemCount.title = `${moved} items would move`;
        proposedDescription.textContent = message;
        syncSaveControls(moved);
    };

    const reorderProposedRows = (compare, message) => {
        const rows = Array.from(proposedList.children).sort(compare);
        rows.forEach(row => proposedList.append(row));
        updateProposedPositions(message);
    };

    const compareByArtistOrder = (left, right) => {
        const ranks = artistRanks();
        const leftRank = ranks.get(normalize(left.dataset.artistKey)) ?? Number.MAX_SAFE_INTEGER;
        const rightRank = ranks.get(normalize(right.dataset.artistKey)) ?? Number.MAX_SAFE_INTEGER;

        return leftRank - rightRank || originalPosition(left) - originalPosition(right);
    };

    document.querySelector("#alphabetize-artists")?.addEventListener("click", () => {
        Array.from(artistList.children)
            .sort((left, right) => left.dataset.artistName.localeCompare(
                right.dataset.artistName,
                undefined,
                { sensitivity: "base" }))
            .forEach(item => artistList.append(item));
        refreshArtistIndices();
    });

    artistFind?.addEventListener("input", findArtists);
    artistFind?.addEventListener("keydown", event => {
        if (event.key !== "Enter" || matchingArtists.length === 0) {
            return;
        }

        event.preventDefault();
        showArtistMatch(currentArtistMatchIndex + (event.shiftKey ? -1 : 1));
    });

    artistList.addEventListener("dragstart", event => {
        if (event.target.closest("input, button")) {
            event.preventDefault();
            return;
        }

        draggedArtist = event.target.closest(".artist-order-item");
        if (!draggedArtist) {
            return;
        }

        draggedArtist.classList.add("dragging");
        event.dataTransfer.effectAllowed = "move";
    });

    artistList.addEventListener("dragover", event => {
        if (!draggedArtist) {
            return;
        }

        event.preventDefault();
        const target = event.target.closest(".artist-order-item");
        if (!target || target === draggedArtist) {
            return;
        }

        const bounds = target.getBoundingClientRect();
        const insertAfter = event.clientY > bounds.top + bounds.height / 2;
        artistList.insertBefore(draggedArtist, insertAfter ? target.nextSibling : target);
        refreshArtistIndices();
    });

    artistList.addEventListener("dragend", () => {
        draggedArtist?.classList.remove("dragging");
        draggedArtist = null;
    });

    artistList.addEventListener("click", event => {
        const button = event.target.closest("[data-move]");
        const item = button?.closest(".artist-order-item");
        if (!button || !item) {
            return;
        }

        if (button.dataset.move === "up" && item.previousElementSibling) {
            artistList.insertBefore(item, item.previousElementSibling);
        }
        else if (button.dataset.move === "down" && item.nextElementSibling) {
            artistList.insertBefore(item.nextElementSibling, item);
        }

        refreshArtistIndices();
    });

    const moveArtistToPosition = (positionInput, restoreFocus = false) => {
        const item = positionInput?.closest(".artist-order-item");
        if (!positionInput || !item) {
            return;
        }

        const itemCount = artistList.children.length;
        const parsedPosition = Number.parseInt(positionInput.value, 10);
        const requestedPosition = Number.isNaN(parsedPosition)
            ? Array.from(artistList.children).indexOf(item) + 1
            : Math.min(Math.max(parsedPosition, 1), itemCount);
        const requestedIndex = requestedPosition - 1;

        item.remove();
        const remainingItems = Array.from(artistList.children);
        artistList.insertBefore(item, remainingItems[requestedIndex] ?? null);
        refreshArtistIndices();
        if (restoreFocus) {
            positionInput.focus();
            positionInput.select();
        }
    };

    artistList.addEventListener("change", event => {
        clearTimeout(positionChangeTimeout);
        moveArtistToPosition(event.target.closest(".artist-order-index"));
    });

    artistList.addEventListener("focusout", event => {
        clearTimeout(positionChangeTimeout);
        moveArtistToPosition(event.target.closest(".artist-order-index"));
    });

    artistList.addEventListener("input", event => {
        const positionInput = event.target.closest(".artist-order-index");
        if (!positionInput) {
            return;
        }

        clearTimeout(positionChangeTimeout);
        positionChangeTimeout = setTimeout(
            () => moveArtistToPosition(positionInput, true),
            400);
    });

    artistList.addEventListener("keydown", event => {
        if (event.key === "Enter" && event.target.matches(".artist-order-index")) {
            event.preventDefault();
            clearTimeout(positionChangeTimeout);
            moveArtistToPosition(event.target, true);
        }
    });

    document.querySelector("#apply-artist-order")?.addEventListener("click", () => {
        appliedArtistOrder = Array.from(artistList.children)
            .map(item => item.dataset.artistName);
        reorderProposedRows(
            compareByArtistOrder,
            "Grouped by your artist order; each artist’s tracks retain their previous order.");
    });

    document.querySelector("#artistOrderModal")?.addEventListener("show.bs.modal", () => {
        renderArtistOrder(appliedArtistOrder);
        if (artistFind) {
            artistFind.value = "";
        }
        clearArtistSearch();
    });

    document.querySelector("#sort-by-album")?.addEventListener("click", () => {
        const ranks = artistRanks();
        const numericValue = value => {
            const number = Number.parseInt(value, 10);
            return number > 0 ? number : Number.MAX_SAFE_INTEGER;
        };
        const sortableReleaseDate = value => {
            const match = /^(\d{4})(?:-(\d{2}))?(?:-(\d{2}))?$/.exec(value ?? "");
            return match
                ? `${match[1]}-${match[2] ?? "01"}-${match[3] ?? "01"}`
                : "9999-12-31";
        };

        reorderProposedRows((left, right) => {
            const leftArtistRank = ranks.get(normalize(left.dataset.artistKey)) ?? Number.MAX_SAFE_INTEGER;
            const rightArtistRank = ranks.get(normalize(right.dataset.artistKey)) ?? Number.MAX_SAFE_INTEGER;

            if (leftArtistRank === Number.MAX_SAFE_INTEGER && rightArtistRank === Number.MAX_SAFE_INTEGER) {
                return originalPosition(left) - originalPosition(right);
            }

            return leftArtistRank - rightArtistRank
                || sortableReleaseDate(left.dataset.albumReleaseDate).localeCompare(
                    sortableReleaseDate(right.dataset.albumReleaseDate))
                || (left.dataset.albumName ?? "").localeCompare(
                    right.dataset.albumName ?? "",
                    undefined,
                    { sensitivity: "base" })
                || numericValue(left.dataset.discNumber) - numericValue(right.dataset.discNumber)
                || numericValue(left.dataset.trackNumber) - numericValue(right.dataset.trackNumber)
                || originalPosition(left) - originalPosition(right);
        }, "Grouped by your artist order, then by album release date (earliest first), disc, and track number.");
    });

    document.querySelector("#reset-preview")?.addEventListener("click", () => {
        appliedArtistOrder = [...originalArtistOrder];
        renderArtistOrder(appliedArtistOrder);

        reorderProposedRows(
            (left, right) => originalPosition(left) - originalPosition(right),
            "Matches the current order until you choose a sort.");
    });

    syncSaveControls(0);
})();

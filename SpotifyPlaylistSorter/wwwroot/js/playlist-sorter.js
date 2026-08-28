(() => {
    const proposedList = document.querySelector("#proposed-track-list");
    const artistList = document.querySelector("[data-artist-order-list]");
    const movedItemCount = document.querySelector("#moved-item-count");
    const proposedDescription = document.querySelector("#proposed-order-description");

    if (!proposedList || !artistList || !movedItemCount || !proposedDescription) {
        return;
    }

    const normalize = value => (value ?? "").trim().toLocaleLowerCase();
    const originalArtistOrder = Array.from(artistList.children)
        .map(item => item.dataset.artistName);
    let appliedArtistOrder = [...originalArtistOrder];
    let draggedArtist = null;

    const originalPosition = row => Number.parseInt(row.dataset.originalPosition, 10);
    const artistRanks = () => new Map(
        appliedArtistOrder.map((name, index) => [normalize(name), index]));
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

    artistList.addEventListener("change", event => {
        const positionInput = event.target.closest(".artist-order-index");
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
        positionInput.focus();
        positionInput.select();
    });

    artistList.addEventListener("keydown", event => {
        if (event.key === "Enter" && event.target.matches(".artist-order-index")) {
            event.preventDefault();
            event.target.blur();
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
    });

    document.querySelector("#sort-by-album")?.addEventListener("click", () => {
        const ranks = artistRanks();
        const numericValue = value => {
            const number = Number.parseInt(value, 10);
            return number > 0 ? number : Number.MAX_SAFE_INTEGER;
        };

        reorderProposedRows((left, right) => {
            const leftArtistRank = ranks.get(normalize(left.dataset.artistKey)) ?? Number.MAX_SAFE_INTEGER;
            const rightArtistRank = ranks.get(normalize(right.dataset.artistKey)) ?? Number.MAX_SAFE_INTEGER;

            if (leftArtistRank === Number.MAX_SAFE_INTEGER && rightArtistRank === Number.MAX_SAFE_INTEGER) {
                return originalPosition(left) - originalPosition(right);
            }

            return leftArtistRank - rightArtistRank
                || (left.dataset.albumName ?? "").localeCompare(
                    right.dataset.albumName ?? "",
                    undefined,
                    { sensitivity: "base" })
                || numericValue(left.dataset.discNumber) - numericValue(right.dataset.discNumber)
                || numericValue(left.dataset.trackNumber) - numericValue(right.dataset.trackNumber)
                || originalPosition(left) - originalPosition(right);
        }, "Grouped by your artist order, then by album, disc, and track number.");
    });

    document.querySelector("#reset-preview")?.addEventListener("click", () => {
        appliedArtistOrder = [...originalArtistOrder];
        renderArtistOrder(appliedArtistOrder);

        reorderProposedRows(
            (left, right) => originalPosition(left) - originalPosition(right),
            "Matches the current order until you choose a sort.");
    });
})();
